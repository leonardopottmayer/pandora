using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class PageAppearanceTests
{
    [Fact]
    public void Goes_to_json_and_back()
    {
        var media = Guid.NewGuid();
        Assert.True(PageAppearance.TryCreate(new PageCover(media, null, 20), "wide", "mono", true, out var look, out _));

        var read = PageAppearance.Parse(look.ToJson());
        Assert.Equal(look, read);
        Assert.Equal(media, read.CoverMediaId);
    }

    [Theory]
    [InlineData("huge", "default", null, null)]
    [InlineData("normal", "comic", null, null)]
    [InlineData("normal", "default", "neon", null)]
    [InlineData("normal", "default", "ocean", "0198f6a2-0000-7000-8000-000000000001")]
    public void Refuses_what_it_does_not_know(string width, string font, string? gradient, string? media)
    {
        var cover = gradient is null && media is null ? null : new PageCover(media is null ? null : Guid.Parse(media), gradient);
        Assert.False(PageAppearance.TryCreate(cover, width, font, false, out _, out var problem));
        Assert.NotNull(problem);
    }

    [Fact]
    public void Reads_nothing_or_garbage_as_the_default_look()
    {
        Assert.Equal(PageAppearance.Default, PageAppearance.Parse(null));
        Assert.Equal(PageAppearance.Default, PageAppearance.Parse("not json"));
    }
}
