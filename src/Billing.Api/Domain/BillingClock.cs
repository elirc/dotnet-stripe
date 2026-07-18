namespace Billing.Api.Domain;

public interface IBillingClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemBillingClock : IBillingClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

