using System.Text;
using Billing.Api.Domain;

namespace Billing.Api.Infrastructure;

/// <summary>HTTP parsing only. Durable receipts live in BillingStore's transaction.</summary>
public static class IdempotencyMiddleware
{
    public static string Token(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.Ordinal) ? header[7..] : "";
    }
    public static long Version(HttpRequest request)
    {
        var value = request.Headers.IfMatch.ToString();
        if (value.Length == 0) throw new BillingError(428, "precondition", "Send the current ETag in If-Match.");
        if (value.Length < 4 || !value.StartsWith("\"v", StringComparison.Ordinal) || !value.EndsWith('"') ||
            !long.TryParse(value.AsSpan(2, value.Length - 3), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var version) || version < 1 || version >= long.MaxValue)
            throw new BillingError(400, "precondition", "If-Match must contain one strong ETag, such as \"v1\".");
        return version;
    }
    public static Mutation Request(HttpRequest request, string body, bool needsVersion = false)
    {
        var key = request.Headers["Idempotency-Key"].ToString();
        if (key.Length is < 8 or > 100 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new BillingError(400, "idempotency", "Send an 8–100 character Idempotency-Key using letters, digits, hyphens or underscores.");
        var version = needsVersion ? Version(request) : (long?)null;
        var fingerprint = WorkspaceAuth.Hash(request.Method + "\n" + request.Path + request.QueryString + "\n" +
            request.Headers.IfMatch + "\n" + body);
        return new Mutation(Token(request), key, fingerprint, version);
    }
    public static async Task<string> Body(HttpRequest request)
    {
        if (request.ContentLength > 32_768) throw new BillingError(413, "body", "Request body exceeds 32 KiB.");
        if (request.Method != "DELETE" && !(request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) ?? false))
            throw new BillingError(415, "content_type", "Use application/json.");
        using var output = new MemoryStream();
        var buffer = new byte[4096];
        int read;
        while ((read = await request.Body.ReadAsync(buffer, request.HttpContext.RequestAborted)) > 0)
        {
            if (output.Length + read > 32_768) throw new BillingError(413, "body", "Request body exceeds 32 KiB.");
            output.Write(buffer, 0, read);
        }
        try { return new UTF8Encoding(false, true).GetString(output.ToArray()); }
        catch (DecoderFallbackException) { throw new BillingError(400, "encoding", "Body must be valid UTF-8."); }
    }
}
