using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class WikilinkParserTests
{
    [Fact]
    public void Finds_targets_in_reading_order()
    {
        var refs = WikilinkParser.Parse("See [[Alpha]] and then [[Beta]].");

        Assert.Equal(["Alpha", "Beta"], refs.Select(r => r.Target));
        Assert.All(refs, r => Assert.Equal(PageLinkKind.Wikilink, r.Kind));
    }

    [Fact]
    public void Alias_is_dropped_and_only_the_target_kept()
    {
        var refs = WikilinkParser.Parse("[[Meeting Notes|yesterday's meeting]]");

        Assert.Equal("Meeting Notes", Assert.Single(refs).Target);
    }

    [Fact]
    public void Bang_prefix_marks_an_embed()
    {
        var refs = WikilinkParser.Parse("![[Diagram]]");

        Assert.Equal(PageLinkKind.Embed, Assert.Single(refs).Kind);
    }

    [Fact]
    public void Same_target_twice_yields_one_reference_per_kind()
    {
        var refs = WikilinkParser.Parse("[[Alpha]] [[alpha]] [[ALPHA]] ![[Alpha]]");

        Assert.Equal(2, refs.Count);
        Assert.Contains(refs, r => r.Kind == PageLinkKind.Wikilink);
        Assert.Contains(refs, r => r.Kind == PageLinkKind.Embed);
    }

    [Fact]
    public void Target_is_trimmed()
    {
        Assert.Equal("Alpha", Assert.Single(WikilinkParser.Parse("[[  Alpha  ]]")).Target);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no links here")]
    [InlineData("a single [bracket] and a [link](http://x)")]
    [InlineData("[[]]")]
    [InlineData("[[   ]]")]
    public void Returns_nothing_when_there_is_no_usable_link(string markdown)
    {
        Assert.Empty(WikilinkParser.Parse(markdown));
    }

    [Fact]
    public void Null_content_is_accepted()
    {
        Assert.Empty(WikilinkParser.Parse(null));
    }
}
