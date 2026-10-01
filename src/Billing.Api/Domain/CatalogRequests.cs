namespace Billing.Api.Domain;

public sealed record CustomerInput(string Name, string Email, string Currency, bool IsArchived = false);
public sealed record PlanInput(string Code, string Name, BillingCycle Cycle, PlanPricingModel PricingModel,
    decimal BasePrice, int IncludedUnits, decimal UnitPrice, string Currency, bool IsActive = true, string Metric = "api_calls");
public sealed record CouponInput(string Code, string Name, decimal? PercentOff, decimal? AmountOff,
    string Currency, DateTimeOffset? ExpiresAt, bool IsActive = true);
public sealed record SubscriptionInput(Guid CustomerId, Guid PlanId, Guid? CouponId);
public sealed record SubscriptionEdit(bool CancelAtPeriodEnd);
public sealed record UsageInput(Guid SubscriptionId, int Quantity, string Metric, DateTimeOffset OccurredAt);
public sealed record CycleInput(DateTimeOffset BillingDate);
public sealed record ProcessInput(bool Succeeded);
public sealed record ProviderInput(string EventId, Guid InvoiceId, Guid PaymentId, bool Succeeded, string ProviderReference);
