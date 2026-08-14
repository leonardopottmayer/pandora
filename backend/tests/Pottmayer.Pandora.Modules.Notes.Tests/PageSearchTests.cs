using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class PageSearchTests
{
    [Theory]
    [InlineData("reuniao", "reuniao:*")]
    [InlineData("  Reuniao  Semanal ", "reuniao:* & semanal:*")]
    [InlineData("notes 2026", "notes:* & 2026:*")]
    public void Every_word_must_match_and_is_a_prefix(string term, string expected)
    {
        Assert.Equal(expected, PageSearch.ToTsQuery(term));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void Term_with_nothing_searchable_yields_no_query(string? term)
    {
        Assert.Equal(string.Empty, PageSearch.ToTsQuery(term));
    }

    [Theory]
    [InlineData("a | b")]
    [InlineData("a & b:*")]
    [InlineData("!(a)")]
    public void Tsquery_operators_typed_by_the_user_are_dropped_not_interpreted(string term)
    {
        // Punctuation never survives, so nothing the user types can become query syntax.
        Assert.DoesNotContain('|', PageSearch.ToTsQuery(term));
        Assert.DoesNotContain('!', PageSearch.ToTsQuery(term));
        Assert.DoesNotContain('(', PageSearch.ToTsQuery(term));
    }

    [Fact]
    public void Excerpt_is_taken_around_the_first_word_of_the_term()
    {
        var content = new string('x', 300) + " agulha " + new string('y', 300);

        var excerpt = PageSearch.Excerpt(content, "agulha");

        Assert.Contains("agulha", excerpt);
        Assert.StartsWith("...", excerpt);
        Assert.EndsWith("...", excerpt);
    }

    [Fact]
    public void Excerpt_falls_back_to_the_head_when_the_hit_was_the_title()
    {
        var excerpt = PageSearch.Excerpt("corpo sem o termo", "titulo");

        Assert.Equal("corpo sem o termo", excerpt);
    }

    [Fact]
    public void Excerpt_collapses_line_breaks_so_it_stays_one_line()
    {
        Assert.Equal("# Titulo corpo", PageSearch.Excerpt("# Titulo\n\n  corpo", "titulo"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Excerpt_of_an_empty_body_is_empty(string? content)
    {
        Assert.Equal(string.Empty, PageSearch.Excerpt(content, "termo"));
    }

    [Fact]
    public void Excerpt_never_exceeds_its_length_plus_the_ellipses()
    {
        var excerpt = PageSearch.Excerpt(new string('z', 1000), "z");

        Assert.Equal(PageSearch.ExcerptLength + 3, excerpt.Length);
    }
}
