using System.Globalization;
using System.Text.Json.Serialization;

namespace Billing.Api.Domain;

public readonly record struct Money
{
    public const long MaximumMinorUnits = 900_000_000_000;
    public static readonly string[] SupportedCurrencies = ["USD", "EUR", "GBP"];
    public long MinorUnits { get; }
    public string Currency { get; }

    public static Money Zero(string currency) => new(0, NormalizeCurrency(currency));

    public static Money FromMajor(decimal amount, string currency)
    {
        if (decimal.Round(amount, 2) != amount)
            throw new ArgumentException("Amounts must have at most two decimal places.", nameof(amount));
        if (amount < -MaximumMinorUnits / 100m || amount > MaximumMinorUnits / 100m)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount exceeds the supported range.");
        return new Money(checked((long)(amount * 100m)), currency);
    }

    [JsonConstructor]
    public Money(long minorUnits, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required.", nameof(currency));
        }

        if (minorUnits < -MaximumMinorUnits || minorUnits > MaximumMinorUnits)
            throw new ArgumentOutOfRangeException(nameof(minorUnits), "Amount exceeds the supported range.");
        MinorUnits = minorUnits;
        Currency = NormalizeCurrency(currency);
    }

    [JsonIgnore]
    public decimal MajorUnits => MinorUnits / 100m;

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(checked(MinorUnits + other.MinorUnits), Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(checked(MinorUnits - other.MinorUnits), Currency);
    }

    public Money Multiply(decimal multiplier)
    {
        var minor = decimal.Round(MinorUnits * multiplier, 0, MidpointRounding.AwayFromZero);
        if (minor < -MaximumMinorUnits || minor > MaximumMinorUnits)
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Calculated amount exceeds the supported range.");
        return new Money(checked((long)minor), Currency);
    }

    public Money Max(Money other)
    {
        EnsureSameCurrency(other);
        return MinorUnits >= other.MinorUnits ? this : other;
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Currency} {MajorUnits:0.00}");

    public static string NormalizeCurrency(string currency)
    {
        var normalized = currency?.Trim().ToUpperInvariant();
        if (normalized is not ("USD" or "EUR" or "GBP"))
            throw new ArgumentException("This workshop supports the two-decimal currencies USD, EUR and GBP.", nameof(currency));
        return normalized;
    }

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Currency mismatch: {Currency} vs {other.Currency}.");
        }
    }
}
