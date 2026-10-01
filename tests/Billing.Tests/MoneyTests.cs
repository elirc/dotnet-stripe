using Billing.Api.Domain;
using Billing.Api.Infrastructure;

namespace Billing.Tests;

public sealed class MoneyTests
{
    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    public void ExactMinorUnitsSurviveJsonRoundTrip(string currency)
    {
        var original = Money.FromMajor(12.34m, currency);
        var restored = BillingJson.Read<Money>(BillingJson.Write(original));
        Assert.Equal(1234, restored.MinorUnits);
        Assert.Equal(currency, restored.Currency);
        Assert.Equal(original, restored);
    }

    [Theory]
    [InlineData("JPY")]
    [InlineData("INVALID")]
    [InlineData("")]
    public void UnsupportedCurrencyIsAnExplicitContract(string currency) =>
        Assert.Throws<ArgumentException>(() => Money.FromMajor(1m, currency));

    [Fact]
    public void InputCannotSilentlyRoundAwayFractionalCents() =>
        Assert.Throws<ArgumentException>(() => Money.FromMajor(0.001m, "USD"));

    [Fact]
    public void DifferentCurrenciesCannotBeAdded() =>
        Assert.Throws<InvalidOperationException>(() => Money.Zero("USD").Add(Money.Zero("EUR")));

    [Fact]
    public void AdditionCannotOverflowTheBrowserSafeRange() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(Money.MaximumMinorUnits, "USD").Add(new Money(1, "USD")));

    [Fact]
    public void PercentageRoundingIsExplicitAndStable()
    {
        Assert.Equal(101, Money.FromMajor(2.01m, "USD").Multiply(.5m).MinorUnits);
        Assert.Equal(-101, Money.FromMajor(-2.01m, "USD").Multiply(.5m).MinorUnits);
    }

    [Fact]
    public void ExcessiveInputIsRejectedBeforeDecimalMultiplication() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.FromMajor(decimal.MaxValue, "USD"));
}
