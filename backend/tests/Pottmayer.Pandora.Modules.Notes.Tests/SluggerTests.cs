using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class SluggerTests
{
    [Theory]
    [InlineData("My Note", "my-note")]
    [InlineData("  Trimmed  ", "trimmed")]
    [InlineData("Café com Pão", "cafe-com-pao")]
    [InlineData("Multiple   spaces & symbols!!!", "multiple-spaces-symbols")]
    [InlineData("---leading and trailing---", "leading-and-trailing")]
    public void Produces_clean_slug(string title, string expected)
    {
        Assert.Equal(expected, Slugger.From(title));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void Falls_back_to_untitled_when_nothing_usable(string title)
    {
        Assert.Equal("untitled", Slugger.From(title));
    }
}
