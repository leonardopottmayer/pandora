using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class TagParserTests
{
    [Fact]
    public void Finds_tags_in_reading_order()
    {
        var tags = TagParser.Parse("Sobre #ideias e também #pandora.");

        Assert.Equal(["ideias", "pandora"], tags.Select(t => t.Slug));
    }

    [Fact]
    public void Same_tag_written_twice_yields_one_reference()
    {
        var tags = TagParser.Parse("#Café pela manhã, #cafe à tarde, #CAFÉ à noite");

        Assert.Equal("cafe", Assert.Single(tags).Slug);
    }

    [Fact]
    public void Display_name_keeps_how_it_was_first_written()
    {
        var tags = TagParser.Parse("#Café e #cafe");

        Assert.Equal("Café", Assert.Single(tags).Name);
    }

    [Fact]
    public void Heading_is_not_a_tag()
    {
        Assert.Empty(TagParser.Parse("# Título da page\n\nCorpo."));
    }

    [Fact]
    public void Hash_glued_to_other_text_is_not_a_tag()
    {
        Assert.Empty(TagParser.Parse("veja https://exemplo.com/doc#secao e src/lib#2"));
    }

    [Fact]
    public void Digits_only_is_not_a_tag()
    {
        Assert.Empty(TagParser.Parse("issue #123 e #2026"));
    }

    [Fact]
    public void Fenced_code_is_not_read()
    {
        const string markdown = """
            Antes de tudo #real

            ```bash
            #naovale
            echo "#tambemnao"
            ```

            Depois #outra
            """;

        Assert.Equal(["real", "outra"], TagParser.Parse(markdown).Select(t => t.Slug));
    }

    [Fact]
    public void Inline_code_is_not_read()
    {
        var tags = TagParser.Parse("use `#naovale` mas #vale sim");

        Assert.Equal("vale", Assert.Single(tags).Slug);
    }

    [Fact]
    public void Nested_tag_keeps_its_slash()
    {
        var tags = TagParser.Parse("#projeto/pandora");

        Assert.Equal("projeto/pandora", Assert.Single(tags).Slug);
    }

    [Fact]
    public void Tag_at_the_start_of_a_line_is_found()
    {
        var tags = TagParser.Parse("linha um\n#inicio da linha dois");

        Assert.Equal("inicio", Assert.Single(tags).Slug);
    }

    [Fact]
    public void Punctuation_ends_the_tag()
    {
        var tags = TagParser.Parse("fim de frase com #tag, e #outra.");

        Assert.Equal(["tag", "outra"], tags.Select(t => t.Slug));
    }

    [Fact]
    public void Empty_content_has_no_tags()
    {
        Assert.Empty(TagParser.Parse(null));
        Assert.Empty(TagParser.Parse("   "));
    }
}
