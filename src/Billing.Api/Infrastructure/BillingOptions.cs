namespace Billing.Api.Infrastructure;

// Runtime location is configured through Billing:Database (environment: Billing__Database).
// Workspace webhook secrets are generated randomly at registration and stored in the
// private database. There is no shared default secret or unattended worker interval.
public static class BillingOptions
{
    public const string DatabaseConfigurationKey = "Billing:Database";
}
