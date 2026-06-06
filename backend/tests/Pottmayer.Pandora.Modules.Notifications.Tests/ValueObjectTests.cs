using Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notifications.Tests;

public sealed class ChannelTests
{
    [Fact]
    public void FromValue_returns_the_email_singleton()
    {
        Assert.Same(Channel.Email, Channel.FromValue("email"));
        Assert.Equal("email", Channel.Email.Value);
        Assert.Equal("email", Channel.Email.ToString());
    }

    [Theory]
    [InlineData("sms")]
    [InlineData("Email")]
    [InlineData("")]
    public void FromValue_rejects_unknown_channels(string value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Channel.FromValue(value));
    }
}

public sealed class TemplateKeyTests
{
    [Fact]
    public void Create_trims_and_lowercases()
    {
        var key = TemplateKey.Create("  Account-Activation  ");

        Assert.Equal("account-activation", key.Value);
        Assert.Equal("account-activation", key.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_keys(string? raw)
    {
        Assert.Throws<ArgumentException>(() => TemplateKey.Create(raw!));
    }

    [Fact]
    public void FromValue_matches_Create()
    {
        Assert.Equal(TemplateKey.Create("welcome"), TemplateKey.FromValue("WELCOME"));
    }
}
