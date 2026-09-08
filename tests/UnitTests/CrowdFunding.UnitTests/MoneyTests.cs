using CrowdFunding.BuildingBlocks.Domain.ValueObjects;

namespace CrowdFunding.UnitTests;

public sealed class MoneyTests
{
    [Fact]
    public void Constructor_ShouldRoundAmountAndUppercaseCurrency()
    {
        var money = new Money(12.345m, "usd");

        Assert.Equal(12.35m, money.Amount);
        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenAmountIsNegative()
    {
        var action = () => new Money(-1m, "USD");

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Money amount cannot be negative. (Parameter 'amount')", exception.Message);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCurrencyIsMissing()
    {
        var action = () => new Money(10m, "   ");

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Currency is required. (Parameter 'currency')", exception.Message);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCurrencyIsNotThreeLetters()
    {
        var action = () => new Money(10m, "US");

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Equal("Currency must be a 3-letter ISO code. (Parameter 'currency')", exception.Message);
    }

    [Fact]
    public void Add_ShouldSumAmounts_WhenCurrenciesMatch()
    {
        var result = new Money(10m, "USD").Add(new Money(5m, "usd"));

        Assert.Equal(new Money(15m, "USD"), result);
    }

    [Fact]
    public void Add_ShouldThrow_WhenCurrenciesMismatch()
    {
        var action = () => new Money(10m, "USD").Add(new Money(5m, "EUR"));

        var exception = Assert.Throws<InvalidOperationException>(action);

        Assert.Equal("Money currency mismatch.", exception.Message);
    }

    [Fact]
    public void Subtract_ShouldReduceAmount_WhenCurrenciesMatch()
    {
        var result = new Money(10m, "USD").Subtract(new Money(4m, "USD"));

        Assert.Equal(new Money(6m, "USD"), result);
    }

    [Fact]
    public void Subtract_ShouldThrow_WhenCurrenciesMismatch()
    {
        var action = () => new Money(10m, "USD").Subtract(new Money(4m, "EUR"));

        var exception = Assert.Throws<InvalidOperationException>(action);

        Assert.Equal("Money currency mismatch.", exception.Message);
    }

    [Fact]
    public void Subtract_ShouldThrow_WhenResultWouldBeNegative()
    {
        var action = () => new Money(4m, "USD").Subtract(new Money(10m, "USD"));

        var exception = Assert.Throws<InvalidOperationException>(action);

        Assert.Equal("Cannot subtract more money than available.", exception.Message);
    }

    [Fact]
    public void Zero_ShouldCreateZeroAmountInGivenCurrency()
    {
        var money = Money.Zero("eur");

        Assert.Equal(0m, money.Amount);
        Assert.Equal("EUR", money.Currency);
    }

    [Fact]
    public void Equals_ShouldReturnTrue_ForSameAmountAndCurrency()
    {
        Assert.Equal(new Money(10m, "USD"), new Money(10m, "usd"));
        Assert.True(new Money(10m, "USD").Equals((object)new Money(10m, "USD")));
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenComparedAgainstNull()
    {
        Assert.False(new Money(10m, "USD").Equals(null));
        Assert.False(new Money(10m, "USD").Equals((object?)null));
    }

    [Fact]
    public void Equals_ShouldReturnFalse_ForDifferentAmountOrCurrency()
    {
        Assert.NotEqual(new Money(10m, "USD"), new Money(11m, "USD"));
        Assert.NotEqual(new Money(10m, "USD"), new Money(10m, "EUR"));
    }

    [Fact]
    public void GetHashCode_ShouldMatch_ForEqualValues()
    {
        Assert.Equal(new Money(10m, "USD").GetHashCode(), new Money(10m, "usd").GetHashCode());
    }

    [Fact]
    public void ToString_ShouldFormatAmountAndCurrency()
    {
        Assert.Equal("10.50 USD", new Money(10.5m, "USD").ToString());
    }
}
