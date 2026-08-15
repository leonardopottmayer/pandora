using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class TagNameTests
{
    [Theory]
    [InlineData("Café", "cafe")]
    [InlineData("#Café", "cafe")]
    [InlineData("PROJETO/Pandora", "projeto/pandora")]
    [InlineData("com_underline", "com_underline")]
    [InlineData("com-hifen", "com-hifen")]
    public void Slug_lowercases_and_strips_accents_keeping_separators(string raw, string expected)
        => Assert.Equal(expected, TagName.ToSlug(raw));

    [Theory]
    [InlineData("123")]
    [InlineData("---")]
    [InlineData("")]
    [InlineData(null)]
    public void Slug_of_something_without_a_letter_is_empty(string? raw)
        => Assert.Empty(TagName.ToSlug(raw));

    [Fact]
    public void Slug_drops_the_separators_at_the_edges()
        => Assert.Equal("meio", TagName.ToSlug("-/meio/_"));

    [Fact]
    public void Slug_is_capped_in_length()
        => Assert.Equal(TagName.MaxLength, TagName.ToSlug(new string('a', 200)).Length);

    [Fact]
    public void Display_name_keeps_the_case_and_drops_the_hash()
        => Assert.Equal("Café", TagName.ToDisplayName("#Café"));
}
