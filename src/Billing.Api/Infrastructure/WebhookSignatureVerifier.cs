using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Billing.Api.Domain;

namespace Billing.Api.Infrastructure;

/// <summary>Workshop HMAC protocol; this is not a Stripe-compatible signature format.</summary>
public static class WebhookSignatureVerifier
{
    public static string Sign(string secret, string timestamp, string body) =>
        Convert.ToHexString(HMACSHA256.HashData(Convert.FromHexString(secret), Encoding.UTF8.GetBytes(timestamp + "." + body)));

    public static void Verify(string secret, string timestamp, string signature, string body, DateTimeOffset now)
    {
        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            seconds < 0 || seconds > 253402300799 || Math.Abs(now.ToUnixTimeSeconds() - seconds) > 300)
            throw new BillingError(401, "signature", "Signature timestamp must be within five minutes of server time.");
        byte[] supplied;
        try { supplied = Convert.FromHexString(signature); }
        catch (FormatException) { throw new BillingError(401, "signature", "Invalid webhook signature."); }
        if (supplied.Length != 32 || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Sign(secret, timestamp, body)), supplied))
            throw new BillingError(401, "signature", "Invalid webhook signature.");
    }
}
