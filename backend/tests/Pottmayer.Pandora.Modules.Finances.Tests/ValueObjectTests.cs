using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class ValueObjectTests
{
    [Theory]
    [InlineData("brl", "BRL")]
    [InlineData("usd", "USD")]
    [InlineData(" btc ", "BTC")]
    [InlineData("usdt", "USDT")]
    public void CurrencyCode_normalizes_valid_codes(string input, string expected)
    {
        Assert.True(CurrencyCode.TryCreate(input, out var currency));
        Assert.Equal(expected, currency!.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("US")]      // too short
    [InlineData("US1")]     // digits not allowed
    [InlineData("REALLYLONGCODE")]
    public void CurrencyCode_rejects_invalid_codes(string? input)
    {
        Assert.False(CurrencyCode.TryCreate(input, out _));
    }

    [Fact]
    public void Money_adds_and_subtracts_within_one_currency()
    {
        var brl = CurrencyCode.Create("BRL");
        var a = new Money(10m, brl);
        var b = new Money(2.5m, brl);

        Assert.Equal(12.5m, a.Add(b).Amount);
        Assert.Equal(7.5m, a.Subtract(b).Amount);
    }

    [Fact]
    public void Money_refuses_mixed_currencies()
    {
        var a = new Money(10m, CurrencyCode.Create("BRL"));
        var b = new Money(10m, CurrencyCode.Create("USD"));

        Assert.Throws<InvalidOperationException>(() => a.Add(b));
        Assert.Throws<InvalidOperationException>(() => a.Subtract(b));
    }

    [Theory]
    [InlineData("cash", true)]
    [InlineData("investment", true)]
    [InlineData("debit", false)]
    [InlineData(null, false)]
    public void AccountType_support_check(string? value, bool supported)
    {
        Assert.Equal(supported, AccountType.IsSupported(value));
    }
}
