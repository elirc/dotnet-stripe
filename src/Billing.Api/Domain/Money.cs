using System.Globalization;

namespace Billing.Api.Domain;

public readonly record struct Money
{
    public long MinorUnits { get; }
    public string Currency { get; }

    public static Money Zero(string currency) => new(0, NormalizeCurrency(currency));

    public static Money FromMajor(decimal amount, string currency)
    {
        var minor = decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);
        return new Money((long)minor, currency);
    }

    public Money(long minorUnits, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required.", nameof(currency));
        }

        MinorUnits = minorUnits;
        Currency = NormalizeCurrency(currency);
    }

    public decimal MajorUnits => MinorUnits / 100m;

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(MinorUnits + other.MinorUnits, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(MinorUnits - other.MinorUnits, Currency);
    }

    public Money Multiply(decimal multiplier)
    {
        var minor = decimal.Round(MinorUnits * multiplier, 0, MidpointRounding.AwayFromZero);
        return new Money((long)minor, Currency);
    }

    public Money Max(Money other)
    {
        EnsureSameCurrency(other);
        return MinorUnits >= other.MinorUnits ? this : other;
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Currency} {MajorUnits:0.00}");

    private static string NormalizeCurrency(string currency) =>
        currency.Trim().ToUpperInvariant();

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Currency mismatch: {Currency} vs {other.Currency}.");
        }
    }
}
