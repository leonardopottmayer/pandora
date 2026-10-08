using Pottmayer.Pandora.Modules.Files.Agent;
using Xunit;

namespace Pottmayer.Pandora.Desktop.Files.Tests;

/// <summary>The walk over a real (temporary) folder tree, pruned by the shared rules.</summary>
public sealed class WalkerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pandora-walk-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Touch(string relative, string content = "x")
    {
        var full = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private List<string> Walk(CatalogRules rules, bool includeHidden = false) =>
        [.. Walker.Walk(_root, rules, includeHidden, CancellationToken.None).Select(i => i.Path).Order(StringComparer.Ordinal)];

    [Fact]
    public void Only_the_selection_is_walked_and_the_way_down_is_kept()
    {
        Touch("A/a.mkv");
        Touch("C/c.mkv");
        Touch("C/Sub/s.mkv");
        Touch("C/Other/o.mkv");

        var rules = new CatalogRules(false, [new MarkConfig("/C", AgentValues.Exclude), new MarkConfig("/C/Sub", AgentValues.Include)], []);

        Assert.Equal(["/A", "/A/a.mkv", "/C", "/C/Sub", "/C/Sub/s.mkv"], Walk(rules));
    }

    [Fact]
    public void Files_carry_their_stat_and_folders_do_not()
    {
        Touch("Movies/a.mkv", "12345");

        var items = Walker.Walk(_root, new CatalogRules(false, [], []), false, CancellationToken.None).ToDictionary(i => i.Path);

        Assert.True(items["/Movies"].IsDirectory);
        Assert.Equal(5, items["/Movies/a.mkv"].Size);
        Assert.Equal(AgentValues.Directory, items["/Movies"].ToEntry(null).Kind);
        Assert.Null(items["/Movies"].ToEntry(null).ModifiedAt);
    }

    [Fact]
    public void Hidden_files_are_skipped_unless_the_root_includes_them()
    {
        Touch("visible.txt");
        Touch("secret.txt");
        File.SetAttributes(Path.Combine(_root, "secret.txt"), FileAttributes.Hidden);
        var rules = new CatalogRules(false, [], []);

        Assert.Equal(["/visible.txt"], Walk(rules));
        Assert.Equal(["/secret.txt", "/visible.txt"], Walk(rules, includeHidden: true));
    }

    [Fact]
    public void A_folder_filter_prunes_its_branch()
    {
        Touch("app/index.js");
        Touch("app/node_modules/lib/x.js");
        var rules = new CatalogRules(false, [], [new FilterConfig(AgentValues.Exclude, AgentValues.Folder, NameMatcher.Glob, "node_modules", null, null)]);

        Assert.Equal(["/app", "/app/index.js"], Walk(rules));
    }

    [Fact]
    public void Folders_of_a_root_are_listed_with_their_catalog_paths()
    {
        Touch("Fotos/2026/a.jpg");
        var root = new RootConfig(Guid.NewGuid(), "Disk", _root, false, false, null, null, [], []);

        var folders = FolderListing.ListInRoot(root, "/Fotos");

        var only = Assert.Single(folders);
        Assert.Equal(("2026", "/Fotos/2026"), (only.Name, only.CatalogPath));
        Assert.Throws<ArgumentException>(() => FolderListing.Resolve(root, "/../outside"));
    }
}
