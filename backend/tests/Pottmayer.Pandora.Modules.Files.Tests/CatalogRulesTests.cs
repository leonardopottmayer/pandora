using Pottmayer.Pandora.Modules.Files.Agent;
using Xunit;

namespace Pottmayer.Pandora.Modules.Files.Tests;

/// <summary>The selection and filter rules of product-plan §4.3, shared by the agent and the backend.</summary>
public sealed class CatalogRulesTests
{
    private static MarkConfig In(string path) => new(path, AgentValues.Include);
    private static MarkConfig Out(string path) => new(path, AgentValues.Exclude);

    private static FilterConfig Filter(string action, string appliesTo, string matcher, string pattern, string? scope = null, bool? caseSensitive = null) =>
        new(action, appliesTo, matcher, pattern, caseSensitive, scope);

    private static CatalogRules Rules(MarkConfig[] marks, params FilterConfig[] filters) => new(false, marks, filters);

    [Fact]
    public void No_marks_include_everything()
    {
        var rules = Rules([]);

        Assert.True(rules.IncludesFolder("/"));
        Assert.True(rules.IncludesFolder("/A/B"));
        Assert.True(rules.IncludesFile("/A/B/c.txt"));
    }

    [Fact]
    public void Folders_A_and_B_but_not_C()
    {
        var rules = Rules([Out("/"), In("/A"), In("/B")]);

        Assert.True(rules.IncludesFolder("/"));          // walked: the way down to A and B
        Assert.True(rules.IncludesFile("/A/x.mkv"));
        Assert.True(rules.IncludesFile("/B/deep/y.mkv"));
        Assert.False(rules.IncludesFolder("/C"));
        Assert.False(rules.IncludesFile("/C/z.mkv"));
        Assert.False(rules.IncludesFile("/top.txt"));
    }

    [Fact]
    public void Only_Sub_inside_C()
    {
        var rules = Rules([Out("/C"), In("/C/Sub")]);

        Assert.True(rules.IncludesFile("/A/a.txt"));
        Assert.True(rules.IncludesFolder("/C"));         // kept to reach Sub
        Assert.False(rules.IncludesFile("/C/c.txt"));
        Assert.False(rules.IncludesFolder("/C/Other"));
        Assert.True(rules.IncludesFolder("/C/Sub"));
        Assert.True(rules.IncludesFile("/C/Sub/deeper/s.txt"));
    }

    [Fact]
    public void Marks_follow_the_roots_case_setting()
    {
        var insensitive = new CatalogRules(false, [Out("/Windows")], []);
        var sensitive = new CatalogRules(true, [Out("/Windows")], []);

        Assert.False(insensitive.IncludesFolder("/WINDOWS"));
        Assert.True(sensitive.IncludesFolder("/WINDOWS"));
    }

    [Fact]
    public void An_include_filter_keeps_only_what_matches()
    {
        var rules = Rules([], Filter(AgentValues.Include, AgentValues.File, NameMatcher.Extension, "mkv, .MP4"));

        Assert.True(rules.IncludesFile("/a.mkv"));
        Assert.True(rules.IncludesFile("/b.mp4"));
        Assert.False(rules.IncludesFile("/c.srt"));
        Assert.True(rules.IncludesFolder("/Series"));    // include filters never drop folders
    }

    [Fact]
    public void Exclude_always_wins_over_include()
    {
        var rules = Rules([],
            Filter(AgentValues.Include, AgentValues.File, NameMatcher.Extension, "mkv"),
            Filter(AgentValues.Exclude, AgentValues.File, NameMatcher.Contains, "sample"));

        Assert.True(rules.IncludesFile("/movie.mkv"));
        Assert.False(rules.IncludesFile("/movie-sample.mkv"));
    }

    [Fact]
    public void A_folder_filter_prunes_everything_below()
    {
        var rules = Rules([In("/code/app/node_modules/keep")],
            Filter(AgentValues.Exclude, AgentValues.Folder, NameMatcher.Glob, "node_modules"));

        Assert.False(rules.IncludesFolder("/code/app/node_modules"));
        Assert.False(rules.IncludesFolder("/code/app/node_modules/keep")); // exclude wins over a mark too
        Assert.False(rules.IncludesFile("/code/app/node_modules/lib/x.js"));
        Assert.True(rules.IncludesFile("/code/app/index.js"));
    }

    [Fact]
    public void A_folder_scoped_filter_only_applies_below_its_folder()
    {
        var rules = Rules([], Filter(AgentValues.Exclude, AgentValues.File, NameMatcher.Extension, "jpg", scope: "/Movies"));

        Assert.False(rules.IncludesFile("/Movies/poster.jpg"));
        Assert.True(rules.IncludesFile("/Photos/poster.jpg"));
    }

    [Fact]
    public void A_glob_with_a_slash_tests_the_path()
    {
        var rules = Rules([], Filter(AgentValues.Exclude, AgentValues.File, NameMatcher.Glob, "**/backup/**"));

        Assert.False(rules.IncludesFile("/backup/a.txt"));
        Assert.False(rules.IncludesFile("/x/y/backup/z/a.txt"));
        Assert.True(rules.IncludesFile("/x/backups/a.txt"));
    }
}
