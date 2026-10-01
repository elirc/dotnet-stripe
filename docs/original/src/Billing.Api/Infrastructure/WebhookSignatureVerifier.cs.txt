using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Billing.Api.Infrastructure;

public sealed class WebhookSignatureVerifier(IOptions<BillingOptions> options)
{
    private static readonly TimeSpan TimestampTolerance = TimeSpan.FromMinutes(5);

    public bool IsValid(HttpRequest request, string rawBody)
    {
        if (!request.Headers.TryGetValue("X-Webhook-Timestamp", out var timestampValues) ||
            !request.Headers.TryGetValue("X-Webhook-Signature", out var signatureValues))
        {
            return false;
        }

        if (!long.TryParse(timestampValues.ToString(), out var unixSeconds))
        {
            return false;
        }

        var timestamp = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        if (DateTimeOffset.UtcNow - timestamp > TimestampTolerance)
        {
            return false;
        }

        var signedPayload = $"{unixSeconds}.{rawBody}";
        var secretBytes = Encoding.UTF8.GetBytes(options.Value.WebhookSigningSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(signedPayload);
        using var hmac = new HMACSHA256(secretBytes);
        var expected = Convert.ToHexString(hmac.ComputeHash(payloadBytes)).ToLowerInvariant();
        var actual = signatureValues.ToString().Trim().ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(actual));
    }
}

