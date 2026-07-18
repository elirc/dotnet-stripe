namespace Billing.Api.Infrastructure;

public sealed class BillingOptions
{
    public string WebhookSigningSecret { get; init; } = "dev_secret_replace_me";
    public int PaymentWorkerIntervalSeconds { get; init; } = 4;
    public int WebhookWorkerIntervalSeconds { get; init; } = 8;
    public int IdempotencyTtlHours { get; init; } = 24;
}

