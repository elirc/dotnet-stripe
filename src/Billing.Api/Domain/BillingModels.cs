namespace Billing.Api.Domain;

public sealed record Customer(
    Guid Id,
    string Name,
    string Email,
    string Currency,
    DateTimeOffset CreatedAt);

public sealed record Plan(
    Guid Id,
    string Code,
    string Name,
    BillingCycle Cycle,
    PlanPricingModel PricingModel,
    Money BasePrice,
    int IncludedUnits,
    Money UnitPrice,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record Coupon(
    Guid Id,
    string Code,
    string Name,
    decimal? PercentOff,
    Money? AmountOff,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt)
{
    public Money CalculateDiscount(Money subtotal, DateTimeOffset now)
    {
        if (ExpiresAt is not null && ExpiresAt <= now)
        {
            return Money.Zero(subtotal.Currency);
        }

        var percentDiscount = PercentOff is null
            ? Money.Zero(subtotal.Currency)
            : subtotal.Multiply(PercentOff.Value / 100m);

        var amountDiscount = AmountOff is null
            ? Money.Zero(subtotal.Currency)
            : AmountOff.Value;

        return percentDiscount.Add(amountDiscount).Max(Money.Zero(subtotal.Currency)).MinorUnits > subtotal.MinorUnits
            ? subtotal
            : percentDiscount.Add(amountDiscount);
    }
}

public sealed record Subscription(
    Guid Id,
    Guid CustomerId,
    Guid PlanId,
    Guid? CouponId,
    SubscriptionStatus Status,
    DateTimeOffset CurrentPeriodStart,
    DateTimeOffset CurrentPeriodEnd,
    DateTimeOffset CreatedAt);

public sealed record UsageEvent(
    Guid Id,
    Guid SubscriptionId,
    int Quantity,
    string Metric,
    DateTimeOffset OccurredAt,
    Guid? InvoiceId,
    DateTimeOffset CreatedAt);

public sealed record InvoiceLine(
    string Description,
    int Quantity,
    Money UnitAmount,
    Money Amount,
    string Kind);

public sealed record Invoice(
    Guid Id,
    string Number,
    Guid CustomerId,
    Guid SubscriptionId,
    InvoiceStatus Status,
    IReadOnlyList<InvoiceLine> Lines,
    Money Subtotal,
    Money Discount,
    Money Total,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    DateTimeOffset DueAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt);

public sealed record Payment(
    Guid Id,
    Guid InvoiceId,
    PaymentStatus Status,
    Money Amount,
    string Provider,
    string? ProviderReference,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record PaymentJob(
    Guid Id,
    Guid InvoiceId,
    int Attempt,
    DateTimeOffset AvailableAt,
    DateTimeOffset CreatedAt);

public sealed record WebhookEvent(
    Guid Id,
    string EventType,
    string Destination,
    string PayloadJson,
    WebhookDeliveryStatus Status,
    int Attempt,
    DateTimeOffset AvailableAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeliveredAt,
    string? LastError);

