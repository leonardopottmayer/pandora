using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Assistant.Tests;

public sealed class ConfirmationLevelTests
{
    [Theory]
    [InlineData("strict")]
    [InlineData("balanced")]
    [InlineData("trusting")]
    public void FromValue_round_trips_each_known_level(string value)
    {
        var level = ConfirmationLevel.FromValue(value);

        Assert.Equal(value, level.Value);
        Assert.Equal(value, level.ToString());
    }

    [Fact]
    public void FromValue_returns_the_singleton_instances()
    {
        Assert.Same(ConfirmationLevel.Strict, ConfirmationLevel.FromValue("strict"));
        Assert.Same(ConfirmationLevel.Balanced, ConfirmationLevel.FromValue("balanced"));
        Assert.Same(ConfirmationLevel.Trusting, ConfirmationLevel.FromValue("trusting"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Strict")]
    [InlineData("aggressive")]
    public void FromValue_rejects_an_unknown_level(string value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ConfirmationLevel.FromValue(value));
    }
}
