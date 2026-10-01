namespace Billing.Api.Domain;

public enum BillingCycle
{
    Monthly,
    Annual
}

public enum PlanPricingModel
{
    Flat,
    Metered,
    Hybrid
}

public enum SubscriptionStatus
{
    Trialing,
    Active,
    PastDue,
    Canceled
}

public enum InvoiceStatus
{
    Draft,
    Open,
    Paid,
    Void,
    Uncollectible
}

public enum PaymentStatus
{
    Pending,
    Succeeded,
    Failed
}

public enum WebhookDeliveryStatus
{
    Pending,
    Succeeded,
    Failed,
    Simulated
}
