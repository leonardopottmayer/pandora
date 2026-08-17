using Pottmayer.Pandora.Modules.Channels.Application.Enqueue;
using Xunit;

namespace Pottmayer.Pandora.Modules.Channels.Tests;

public sealed class LocaleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("fr")]
    [InlineData("pt")]
    public void Normalize_falls_back_to_default(string? input)
    {
        Assert.Equal("en", Locale.Normalize(input));
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("EN", "en")]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("PT-br", "pt-BR")]
    public void Normalize_canonicalizes_supported_locales(string input, string expected)
    {
        Assert.Equal(expected, Locale.Normalize(input));
    }
}
