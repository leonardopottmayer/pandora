using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class BankTests
{
    [Theory]
    [InlineData("341")]
    [InlineData("077")]
    [InlineData("77")]
    [InlineData(" 260 ")]
    public void IsSupported_true_for_known_codes(string code) =>
        Assert.True(Bank.IsSupported(code));

    [Theory]
    [InlineData("999")]
    [InlineData("")]
    [InlineData(null)]
    public void IsSupported_false_for_unknown_or_empty(string? code) =>
        Assert.False(Bank.IsSupported(code));

    [Fact]
    public void FromValue_canonicalizes_leading_zero()
    {
        Assert.Same(Bank.Inter, Bank.FromValue("77"));
        Assert.Equal("077", Bank.FromValue("77").Value);
    }

    [Fact]
    public void Canonicalize_returns_canonical_code_or_null()
    {
        Assert.Equal("077", Bank.Canonicalize("77"));
        Assert.Equal("341", Bank.Canonicalize("341"));
        Assert.Null(Bank.Canonicalize("999"));
        Assert.Null(Bank.Canonicalize(null));
    }

    [Fact]
    public void Matches_tolerates_leading_zero_differences()
    {
        Assert.True(Bank.Matches("077", "77"));
        Assert.True(Bank.Matches("341", "341"));
        Assert.False(Bank.Matches("341", "260"));
        Assert.False(Bank.Matches("341", null));
        Assert.False(Bank.Matches("999", "999"));
    }

    [Fact]
    public void All_has_distinct_codes()
    {
        var codes = Bank.All.Select(b => b.Value).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }
}
