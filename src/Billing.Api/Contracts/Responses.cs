using Billing.Api.Domain;

namespace Billing.Api.Contracts;

public sealed record MoneyDto(long MinorUnits, string Currency, decimal MajorUnits, string Formatted)
{
    public static MoneyDto From(Money money) =>
        new(money.MinorUnits, money.Currency, money.MajorUnits, money.ToString());
}

public sealed record DashboardResponse(
    IReadOnlyList<Customer> Customers,
    IReadOnlyList<Plan> Plans,
    IReadOnlyList<Coupon> Coupons,
    IReadOnlyList<Subscription> Subscriptions,
    IReadOnlyList<InvoiceResponse> Invoices,
    IReadOnlyList<Payment> Payments,
    IReadOnlyList<UsageEvent> UsageEvents,
    IReadOnlyList<WebhookEvent> WebhookEvents,
    DashboardMetrics Metrics);

public sealed record DashboardMetrics(
    int ActiveCustomers,
    int ActiveSubscriptions,
    int OpenInvoices,
    MoneyDto MonthlyRecurringRevenue,
    MoneyDto OutstandingRevenue,
    decimal PaidInvoiceRate);

public sealed record InvoiceLineResponse(
    string Description,
    int Quantity,
    MoneyDto UnitAmount,
    MoneyDto Amount,
    string Kind);

public sealed record InvoiceResponse(
    Guid Id,
    string Number,
    Guid CustomerId,
    Guid SubscriptionId,
    InvoiceStatus Status,
    IReadOnlyList<InvoiceLineResponse> Lines,
    MoneyDto Subtotal,
    MoneyDto Discount,
    MoneyDto Total,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    DateTimeOffset DueAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt)
{
    public static InvoiceResponse From(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.Number,
            invoice.CustomerId,
            invoice.SubscriptionId,
            invoice.Status,
            invoice.Lines.Select(line => new InvoiceLineResponse(
                line.Description,
                line.Quantity,
                MoneyDto.From(line.UnitAmount),
                MoneyDto.From(line.Amount),
                line.Kind)).ToArray(),
            MoneyDto.From(invoice.Subtotal),
            MoneyDto.From(invoice.Discount),
            MoneyDto.From(invoice.Total),
            invoice.PeriodStart,
            invoice.PeriodEnd,
            invoice.DueAt,
            invoice.CreatedAt,
            invoice.PaidAt);
}

public sealed record IdempotencyReplayResponse<T>(bool Replayed, T Result);

