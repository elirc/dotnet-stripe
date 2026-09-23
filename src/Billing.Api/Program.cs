using System.Text.Json;
using System.Text.Json.Serialization;
using Billing.Api.Contracts;
using Billing.Api.Domain;
using Billing.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BillingOptions>(builder.Configuration.GetSection("Billing"));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<IBillingClock, SystemBillingClock>();
builder.Services.AddSingleton<BillingStore>();
builder.Services.AddSingleton<BillingEngine>();
builder.Services.AddSingleton<IdempotencyStore>();
builder.Services.AddSingleton<WebhookSignatureVerifier>();
builder.Services.AddHostedService<PaymentWorker>();
builder.Services.AddHostedService<WebhookDeliveryWorker>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("dashboard", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("dashboard");
app.UseMiddleware<IdempotencyMiddleware>();

var api = app.MapGroup("/api");

api.MapGet("/health", () => Results.Ok(new { status = "ok", timestamp = DateTimeOffset.UtcNow }));

api.MapGet("/dashboard", (BillingStore store) =>
{
    var invoices = store.Invoices();
    var customers = store.Customers();
    var subscriptions = store.Subscriptions();
    var plans = store.Plans();
    var payments = store.Payments();
    var usageEvents = store.UsageEvents();
    var webhookEvents = store.WebhookEvents();
    var coupons = store.Coupons();
    var metrics = CalculateMetrics(customers, subscriptions, plans, invoices);

    return Results.Ok(new DashboardResponse(
        customers,
        plans,
        coupons,
        subscriptions,
        invoices.Select(InvoiceResponse.From).ToArray(),
        payments,
        usageEvents,
        webhookEvents,
        metrics));
});

api.MapGet("/customers", (BillingStore store) => Results.Ok(store.Customers()));

api.MapPost("/customers", (CreateCustomerRequest request, BillingStore store, IBillingClock clock) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
    {
        return BadRequest("Customer name and email are required.");
    }

    try
    {
        var customer = new Customer(Guid.NewGuid(), request.Name.Trim(), request.Email.Trim(), NormalizeCurrency(request.Currency), clock.UtcNow);
        return Results.Created($"/api/customers/{customer.Id}", store.AddCustomer(customer));
    }
    catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
    {
        return BadRequest(ex.Message);
    }
});

api.MapGet("/plans", (BillingStore store) => Results.Ok(store.Plans()));

api.MapPost("/plans", (CreatePlanRequest request, BillingStore store, IBillingClock clock) =>
{
    if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
    {
        return BadRequest("Plan code and name are required.");
    }

    if (request.BasePrice < 0 || request.UnitPrice < 0 || request.IncludedUnits < 0)
    {
        return BadRequest("Plan pricing values cannot be negative.");
    }

    try
    {
        var currency = NormalizeCurrency(request.Currency);
        var plan = new Plan(
            Guid.NewGuid(),
            request.Code.Trim().ToLowerInvariant(),
            request.Name.Trim(),
            request.Cycle,
            request.PricingModel,
            Money.FromMajor(request.BasePrice, currency),
            request.IncludedUnits,
            Money.FromMajor(request.UnitPrice, currency),
            true,
            clock.UtcNow);

        return Results.Created($"/api/plans/{plan.Id}", store.AddPlan(plan));
    }
    catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
    {
        return BadRequest(ex.Message);
    }
});

api.MapGet("/coupons", (BillingStore store) => Results.Ok(store.Coupons()));

api.MapPost("/coupons", (CreateCouponRequest request, BillingStore store, IBillingClock clock) =>
{
    if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
    {
        return BadRequest("Coupon code and name are required.");
    }

    if (request.PercentOff is null && request.AmountOff is null)
    {
        return BadRequest("Coupon needs percentOff or amountOff.");
    }

    if (request.PercentOff is < 0 or > 100 || request.AmountOff is < 0)
    {
        return BadRequest("Coupon discount values are invalid.");
    }

    try
    {
        var amountOff = request.AmountOff is null ? (Money?)null : Money.FromMajor(request.AmountOff.Value, NormalizeCurrency(request.Currency));
        var coupon = new Coupon(Guid.NewGuid(), request.Code.Trim().ToUpperInvariant(), request.Name.Trim(), request.PercentOff, amountOff, request.ExpiresAt, clock.UtcNow);
        return Results.Created($"/api/coupons/{coupon.Id}", store.AddCoupon(coupon));
    }
    catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
    {
        return BadRequest(ex.Message);
    }
});

api.MapGet("/subscriptions", (BillingStore store) => Results.Ok(store.Subscriptions()));

api.MapPost("/subscriptions", (CreateSubscriptionRequest request, BillingStore store, IBillingClock clock) =>
{
    if (!store.TryGetPlan(request.PlanId, out var plan))
    {
        return Results.NotFound(new { error = $"Unknown plan: {request.PlanId}" });
    }

    var periodStart = clock.UtcNow;
    var periodEnd = plan.Cycle == BillingCycle.Annual ? periodStart.AddYears(1) : periodStart.AddMonths(1);
    var subscription = new Subscription(Guid.NewGuid(), request.CustomerId, request.PlanId, request.CouponId, SubscriptionStatus.Active, periodStart, periodEnd, clock.UtcNow);

    try
    {
        return Results.Created($"/api/subscriptions/{subscription.Id}", store.AddSubscription(subscription));
    }
    catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
    {
        return BadRequest(ex.Message);
    }
});

api.MapGet("/invoices", (BillingStore store) => Results.Ok(store.Invoices().Select(InvoiceResponse.From)));

api.MapPost("/usage-events", (CreateUsageEventRequest request, BillingStore store, IBillingClock clock) =>
{
    if (request.Quantity <= 0)
    {
        return BadRequest("Usage quantity must be greater than zero.");
    }

    var usage = new UsageEvent(
        Guid.NewGuid(),
        request.SubscriptionId,
        request.Quantity,
        string.IsNullOrWhiteSpace(request.Metric) ? "api_calls" : request.Metric.Trim(),
        request.OccurredAt ?? clock.UtcNow,
        null,
        clock.UtcNow);

    try
    {
        return Results.Created($"/api/usage-events/{usage.Id}", store.AddUsageEvent(usage));
    }
    catch (KeyNotFoundException ex)
    {
        return BadRequest(ex.Message);
    }
});

api.MapPost("/simulator/run-cycle", (RunCycleRequest request, BillingEngine engine, IBillingClock clock) =>
{
    var billingDate = request.BillingDate ?? clock.UtcNow.AddMonths(1);
    var invoices = engine.RunBillingCycle(billingDate).Select(InvoiceResponse.From).ToArray();
    return Results.Ok(new { generated = invoices.Length, invoices });
});

api.MapPost("/invoices/{invoiceId:guid}/pay", (Guid invoiceId, PayInvoiceRequest request, BillingEngine engine) =>
{
    try
    {
        return Results.Accepted($"/api/invoices/{invoiceId}", engine.QueuePayment(invoiceId, request.Provider));
    }
    catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
    {
        return BadRequest(ex.Message);
    }
});

api.MapPost("/webhooks/payment-provider", async (HttpRequest request, WebhookSignatureVerifier verifier, BillingEngine engine) =>
{
    request.EnableBuffering();
    using var reader = new StreamReader(request.Body, leaveOpen: true);
    var rawBody = await reader.ReadToEndAsync();
    request.Body.Position = 0;

    if (!verifier.IsValid(request, rawBody))
    {
        return Results.Unauthorized();
    }

    var payload = JsonSerializer.Deserialize<PaymentProviderWebhookRequest>(rawBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    if (payload is null)
    {
        return BadRequest("Invalid webhook payload.");
    }

    try
    {
        engine.ApplyPaymentResult(payload.InvoiceId, payload.Succeeded, payload.ProviderReference, payload.FailureReason);
        return Results.Ok(new { received = true, payload.EventId, payload.EventType });
    }
    catch (KeyNotFoundException ex)
    {
        return BadRequest(ex.Message);
    }
});

app.Run();

static DashboardMetrics CalculateMetrics(
    IReadOnlyList<Customer> customers,
    IReadOnlyList<Subscription> subscriptions,
    IReadOnlyList<Plan> plans,
    IReadOnlyList<Invoice> invoices)
{
    var activeSubscriptions = subscriptions.Where(x => x.Status == SubscriptionStatus.Active).ToArray();
    var planLookup = plans.ToDictionary(x => x.Id);
    var mrr = activeSubscriptions.Aggregate(Money.Zero("USD"), (sum, subscription) =>
    {
        if (!planLookup.TryGetValue(subscription.PlanId, out var plan))
        {
            return sum;
        }

        var monthlyAmount = plan.Cycle == BillingCycle.Annual
            ? plan.BasePrice.Multiply(1m / 12m)
            : plan.BasePrice;

        return sum.Add(monthlyAmount);
    });

    var outstanding = invoices
        .Where(x => x.Status == InvoiceStatus.Open)
        .Aggregate(Money.Zero("USD"), (sum, invoice) => sum.Add(invoice.Total));

    var paidCount = invoices.Count(x => x.Status == InvoiceStatus.Paid);
    var paidRate = invoices.Count == 0 ? 0m : decimal.Round((decimal)paidCount / invoices.Count * 100m, 1);

    return new DashboardMetrics(
        customers.Count,
        activeSubscriptions.Length,
        invoices.Count(x => x.Status == InvoiceStatus.Open),
        MoneyDto.From(mrr),
        MoneyDto.From(outstanding),
        paidRate);
}

static string NormalizeCurrency(string currency) =>
    string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();

static IResult BadRequest(string message) =>
    Results.BadRequest(new { error = message });
