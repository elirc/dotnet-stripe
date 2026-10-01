namespace Billing.Api.Domain;

public sealed class BillingError(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;

    public static BillingError Invalid(string message) => new(422, "validation", message);
    public static BillingError Conflict(string message) => new(409, "conflict", message);
    public static BillingError Missing() => new(404, "not_found", "Record not found in this workspace.");
}

public interface IBillingEntity
{
    Guid Id { get; }
    Guid WorkspaceId { get; init; }
    long Version { get; init; }
}
