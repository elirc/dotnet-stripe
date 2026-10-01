using Billing.Api.Domain;

namespace Billing.Api.Contracts;

public sealed record CreateCustomerRequest(string Name, string Email, string Currency);

public sealed record CreatePlanRequest(
    string Code,
    string Name,
    BillingCycle Cycle,
    PlanPricingModel PricingModel,
    decimal BasePrice,
    int IncludedUnits,
    decimal UnitPrice,
    string Currency);

public sealed record CreateCouponRequest(
    string Code,
    string Name,
    decimal? PercentOff,
    decimal? AmountOff,
    string Currency,
    DateTimeOffset? ExpiresAt);

public sealed record CreateSubscriptionRequest(Guid CustomerId, Guid PlanId, Guid? CouponId);

public sealed record CreateUsageEventRequest(Guid SubscriptionId, int Quantity, string Metric, DateTimeOffset? OccurredAt);

public sealed record PayInvoiceRequest(string Provider);

public sealed record RunCycleRequest(DateTimeOffset? BillingDate);

public sealed record PaymentProviderWebhookRequest(
    string EventId,
    string EventType,
    Guid InvoiceId,
    bool Succeeded,
    string? ProviderReference,
    string? FailureReason);

