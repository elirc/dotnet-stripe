using System.Text.Json;
using System.Threading.RateLimiting;
using Billing.Api.Domain;
using Billing.Api.Infrastructure;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 32_768);
builder.Services.AddSingleton(_ => new BillingDatabase(builder.Configuration["Billing:Database"] ??
    Path.Combine(builder.Environment.ContentRootPath, "App_Data", "billing-learning.sqlite")));
builder.Services.AddSingleton<WorkspaceAuth>();
builder.Services.AddSingleton<BillingStore>();
builder.Services.AddSingleton<CatalogService>();
builder.Services.AddSingleton<BillingEngine>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
_ = app.Services.GetRequiredService<BillingDatabase>();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    try
    {
        var host = context.Request.Host.Host;
        if (host is not ("localhost" or "127.0.0.1" or "::1" or "[::1]"))
            throw new BillingError(400, "host", "This workshop accepts loopback hostnames only.");
        var origin = context.Request.Headers.Origin.ToString();
        if (origin.Length > 0 && (!Uri.TryCreate(origin, UriKind.Absolute, out var parsed) ||
            !string.Equals(parsed.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase) ||
            parsed.Scheme != context.Request.Scheme))
            throw new BillingError(403, "origin", "Use the app from its own origin.");
        await next();
    }
    catch (Exception error) when (!context.Response.HasStarted && error is not OperationCanceledException)
    {
        var failure = error switch
        {
            BillingError e => e,
            JsonException => new BillingError(400, "json", "Invalid JSON: check required fields, types, duplicate fields and enum names."),
            BadHttpRequestException e => new BillingError(e.StatusCode, "request", "Invalid request body."),
            SqliteException e when e.SqliteErrorCode == 19 => new BillingError(409, "constraint", "A unique value already exists or this record is referenced. Rename, archive or cancel it instead."),
            SqliteException e when e.SqliteErrorCode is 5 or 6 => new BillingError(503, "busy", "Database is busy. Retry the exact request with its original idempotency key."),
            ArgumentException or OverflowException => BillingError.Invalid("A value is outside its supported format or range."),
            _ => new BillingError(500, "server", "The operation failed. Retry with the same request key.")
        };
        if (failure.StatusCode == 500) app.Logger.LogError(error, "Unhandled request error {Trace}", context.TraceIdentifier);
        context.Response.StatusCode = failure.StatusCode;
        if (failure.StatusCode == 503) context.Response.Headers.RetryAfter = "2";
        await context.Response.WriteAsJsonAsync(new { error = failure.Code, message = failure.Message, traceId = context.TraceIdentifier });
    }
});
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();

IResult Json(object value, int status = 200) => Results.Text(JsonSerializer.Serialize(value, value.GetType(), BillingJson.Options), "application/json", statusCode: status);
string Token(HttpContext c) => IdempotencyMiddleware.Token(c.Request);
IResult Response(HttpContext c, StoredResponse value)
{
    if (value.ETag is not null) c.Response.Headers.ETag = value.ETag;
    if (value.Location is not null) c.Response.Headers.Location = value.Location;
    return Results.Text(value.Json, "application/json", statusCode: value.Status);
}
long Required(Mutation mutation) => mutation.Version ?? throw new InvalidOperationException("Version was not parsed.");

app.MapGet("/api/health", () => Json(new { status = "ok", mode = "local billing simulator", schema = 1 }));
app.MapPost("/api/auth/register", async (HttpContext c, BillingDatabase db, WorkspaceAuth auth) =>
{
    var body = await IdempotencyMiddleware.Body(c.Request);
    return Json(db.Transaction(s => auth.Register(s, BillingJson.Read<RegisterRequest>(body))), 201);
}).RequireRateLimiting("auth");
app.MapPost("/api/auth/login", async (HttpContext c, BillingDatabase db, WorkspaceAuth auth) =>
{
    var body = await IdempotencyMiddleware.Body(c.Request);
    return Json(db.Transaction(s => auth.Login(s, BillingJson.Read<LoginRequest>(body))));
}).RequireRateLimiting("auth");
app.MapPost("/api/auth/logout", (HttpContext c, BillingDatabase db) =>
{
    db.Transaction(s => s.Execute("DELETE FROM sessions WHERE token_hash=$h", ("$h", WorkspaceAuth.Hash(Token(c)))));
    return Json(new { signedOut = true });
});
app.MapGet("/api/auth/me", (HttpContext c, BillingStore store) => Json(store.Read(Token(c), (_, actor) => actor)));
app.MapGet("/api/dashboard", (HttpContext c, BillingStore store) => Json(store.Read(Token(c), (s, actor) =>
{
    var invoices = s.All<Invoice>(actor.WorkspaceId);
    var totals = invoices.GroupBy(i => i.Total.Currency).OrderBy(g => g.Key).Select(group => new
    {
        currency = group.Key,
        openMinorUnits = group.Where(i => i.Status == InvoiceStatus.Open).Sum(i => i.Total.MinorUnits),
        paidMinorUnits = group.Where(i => i.Status == InvoiceStatus.Paid).Sum(i => i.Total.MinorUnits)
    }).ToArray();
    return new { actor, billingTime = CatalogService.BillingTime(s, actor.WorkspaceId), totals,
        counts = new { customers = s.All<Customer>(actor.WorkspaceId).Count, subscriptions = s.All<Subscription>(actor.WorkspaceId).Count, invoices = invoices.Count },
        simulator = true };
})));
app.MapGet("/api/members", (HttpContext c, BillingStore store, WorkspaceAuth auth) => Json(store.Read(Token(c), (s, actor) => auth.Members(s, actor))));
app.MapPost("/api/members", async (HttpContext c, BillingStore store, WorkspaceAuth auth) =>
{
    var body = await IdempotencyMiddleware.Body(c.Request); var request = IdempotencyMiddleware.Request(c.Request, body);
    return Response(c, store.Write(request, "member.created", (s, actor) => auth.AddMember(s, actor, BillingJson.Read<MemberRequest>(body)), 201, owner: true));
});
app.MapMethods("/api/members/{id:guid}", ["PUT", "DELETE"], async (HttpContext c, Guid id, BillingStore store, WorkspaceAuth auth) =>
{
    var body = await IdempotencyMiddleware.Body(c.Request); var request = IdempotencyMiddleware.Request(c.Request, body, true);
    return Response(c, store.Write(request, "member.changed", (s, actor) =>
        auth.ChangeMember(s, actor, id, Required(request), c.Request.Method == "DELETE" ? null : BillingJson.Read<RoleRequest>(body).Role), owner: true));
});
app.MapGet("/api/audit", (HttpContext c, BillingStore store) => Json(store.Read(Token(c), (s, actor) =>
{
    using var command = s.Command("SELECT id,actor,action,record_id,at FROM audit WHERE workspace_id=$w ORDER BY id DESC LIMIT 100", ("$w", actor.WorkspaceId.ToString()));
    using var reader = command.ExecuteReader(); var rows = new List<object>();
    while (reader.Read()) rows.Add(new { id = reader.GetInt64(0), actor = reader.GetString(1), action = reader.GetString(2), recordId = reader.IsDBNull(3) ? null : reader.GetString(3), at = reader.GetString(4) });
    return rows;
})));

foreach (var kind in CatalogService.Kinds)
{
    app.MapGet("/api/" + kind, (HttpContext c, BillingStore store, CatalogService catalog) =>
    {
        int Parse(string key, int fallback) => !c.Request.Query.ContainsKey(key) ? fallback :
            int.TryParse(c.Request.Query[key], out var value) ? value : throw BillingError.Invalid("Pagination must use integers.");
        return Json(store.Read(Token(c), (s, a) => catalog.Page(s, a.WorkspaceId, kind, Parse("offset", 0), Parse("limit", 20), c.Request.Query["search"].ToString())));
    });
    app.MapGet("/api/" + kind + "/{id:guid}", (HttpContext c, Guid id, BillingStore store, CatalogService catalog) =>
    {
        var value = store.Read(Token(c), (s, a) => catalog.Get(s, a.WorkspaceId, kind, id));
        c.Response.Headers.ETag = $"\"v{value.Version}\""; return Json(value);
    });
}
foreach (var kind in new[] { "customers", "plans", "coupons", "subscriptions", "usage-events" })
{
    app.MapPost("/api/" + kind, async (HttpContext c, BillingStore store, CatalogService catalog) =>
    {
        var body = await IdempotencyMiddleware.Body(c.Request); var request = IdempotencyMiddleware.Request(c.Request, body);
        return Response(c, store.Write(request, kind + ".created", (s, a) => kind switch
        {
            "customers" => catalog.Customer(s, a.WorkspaceId, BillingJson.Read<CustomerInput>(body)),
            "plans" => catalog.Plan(s, a.WorkspaceId, BillingJson.Read<PlanInput>(body)),
            "coupons" => catalog.Coupon(s, a.WorkspaceId, BillingJson.Read<CouponInput>(body)),
            "subscriptions" => catalog.Subscribe(s, a.WorkspaceId, BillingJson.Read<SubscriptionInput>(body)),
            "usage-events" => catalog.Usage(s, a.WorkspaceId, BillingJson.Read<UsageInput>(body)),
            _ => throw BillingError.Missing()
        }, 201));
    });
    app.MapMethods("/api/" + kind + "/{id:guid}", ["PUT", "DELETE"], async (HttpContext c, Guid id, BillingStore store, CatalogService catalog) =>
    {
        var body = await IdempotencyMiddleware.Body(c.Request); var request = IdempotencyMiddleware.Request(c.Request, body, true);
        return Response(c, store.Write(request, kind + "." + c.Request.Method.ToLowerInvariant(), (s, a) =>
        {
            if (c.Request.Method == "DELETE") return catalog.Delete(s, a.WorkspaceId, kind, id, Required(request));
            return kind switch
            {
                "customers" => (object)catalog.Customer(s, a.WorkspaceId, BillingJson.Read<CustomerInput>(body), id, Required(request)),
                "plans" => catalog.Plan(s, a.WorkspaceId, BillingJson.Read<PlanInput>(body), id, Required(request)),
                "coupons" => catalog.Coupon(s, a.WorkspaceId, BillingJson.Read<CouponInput>(body), id, Required(request)),
                "subscriptions" => catalog.EditSubscription(s, a.WorkspaceId, id, BillingJson.Read<SubscriptionEdit>(body), Required(request)),
                "usage-events" => catalog.Usage(s, a.WorkspaceId, BillingJson.Read<UsageInput>(body), id, Required(request)),
                _ => throw BillingError.Missing()
            };
        }));
    });
}
app.MapPost("/api/simulator/run-cycle", async (HttpContext c, BillingStore store, BillingEngine engine) =>
{
    var body = await IdempotencyMiddleware.Body(c.Request); var request = IdempotencyMiddleware.Request(c.Request, body);
    return Response(c, store.Write(request, "cycle.advanced", (s, a) => engine.RunCycle(s, a, BillingJson.Read<CycleInput>(body))));
});
app.MapPost("/api/simulator/process", async (HttpContext c, BillingStore store, BillingEngine engine) =>
{
    var body = await IdempotencyMiddleware.Body(c.Request); var request = IdempotencyMiddleware.Request(c.Request, body);
    return Response(c, store.Write(request, "simulation.processed", (s, a) => engine.Process(s, a.WorkspaceId, BillingJson.Read<ProcessInput>(body))));
});
foreach (var action in new[] { "pay", "void" })
    app.MapPost("/api/invoices/{id:guid}/" + action, async (HttpContext c, Guid id, BillingStore store, BillingEngine engine) =>
    {
        var body = await IdempotencyMiddleware.Body(c.Request); var request = IdempotencyMiddleware.Request(c.Request, body, true);
        BillingJson.Read<EmptyInput>(body);
        return Response(c, store.Write(request, "invoice." + action, (s, a) => action == "pay" ?
            (object)engine.QueuePayment(s, a.WorkspaceId, id, Required(request)) : engine.Void(s, a.WorkspaceId, id, Required(request))));
    });
app.MapGet("/api/webhook-lab", (HttpContext c, BillingStore store) => Json(store.Read(Token(c), (s, a) =>
{
    if (a.Role != "Owner") throw new BillingError(403, "permission", "Owner role required.");
    return new { workspaceId = a.WorkspaceId, secret = (string?)s.Scalar("SELECT secret FROM workspaces WHERE id=$w", ("$w", a.WorkspaceId.ToString())),
        protocol = "HMAC-SHA256 hex over timestamp + '.' + exact UTF-8 body; X-Billing-Timestamp and X-Billing-Signature; not Stripe-compatible." };
})));
app.MapPost("/api/webhooks/payment-provider/{workspace:guid}", async (HttpContext c, Guid workspace, BillingDatabase db, BillingEngine engine) =>
{
    var body = await IdempotencyMiddleware.Body(c.Request);
    var result = db.Transaction(s =>
    {
        var secret = s.Scalar("SELECT secret FROM workspaces WHERE id=$w", ("$w", workspace.ToString())) as string ?? throw BillingError.Missing();
        WebhookSignatureVerifier.Verify(secret, c.Request.Headers["X-Billing-Timestamp"].ToString(), c.Request.Headers["X-Billing-Signature"].ToString(), body, DateTimeOffset.UtcNow);
        var input = BillingJson.Read<ProviderInput>(body); var eventId = Input.Text(input.EventId, "Event ID", 100);
        var fingerprint = WorkspaceAuth.Hash(body);
        var prior = s.Scalar("SELECT fingerprint FROM provider_events WHERE workspace_id=$w AND event_id=$id", ("$w", workspace.ToString()), ("$id", eventId)) as string;
        if (prior is not null)
        {
            if (prior != fingerprint) throw BillingError.Conflict("Event ID was already used for another payload.");
            return new { accepted = true, duplicate = true };
        }
        engine.CompletePayment(s, workspace, input.InvoiceId, input.PaymentId, input.Succeeded, input.ProviderReference);
        s.Execute("INSERT INTO provider_events VALUES($w,$id,$fp)", ("$w", workspace.ToString()), ("$id", eventId), ("$fp", fingerprint));
        s.Audit(workspace, "signed-provider-simulator", "payment.callback", input.PaymentId);
        return new { accepted = true, duplicate = false };
    });
    return Json(result);
});
app.MapFallback("/api/{**path}", () => Results.NotFound(new { error = "not_found", message = "API route not found." }));
app.MapFallbackToFile("index.html");
app.Run();

public sealed record EmptyInput;
public partial class Program;
