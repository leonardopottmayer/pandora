namespace Pottmayer.Pandora.Modules.Files.Agent;

/// <summary>
/// What one root catalogs (product-plan §4.3): the selection marks — the deepest mark wins, no mark means
/// included — and the filters on top, where an exclude always wins. The same engine runs in the agent
/// (to prune the walk) and in the backend (to tell a missing entry from an excluded one), so the two
/// never disagree.
/// </summary>
public sealed class CatalogRules
{
    private readonly StringComparison _comparison;
    private readonly Dictionary<string, bool> _marks;
    private readonly List<Rule> _fileIncludes = [];
    private readonly List<Rule> _fileExcludes = [];
    private readonly List<Rule> _folderExcludes = [];

    private sealed record Rule(Func<string, string, bool> Matches, string? ScopePath);

    /// <param name="caseSensitive">The root's setting: drives marks, scopes and the filters' default.</param>
    /// <param name="filters">The enabled filters in scope for the root. A folder filter can only exclude; a folder include is ignored.</param>
    public CatalogRules(bool caseSensitive, IEnumerable<MarkConfig> marks, IEnumerable<FilterConfig> filters)
    {
        _comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        _marks = new Dictionary<string, bool>(caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        foreach (var mark in marks)
            _marks[mark.Path] = mark.Mode == AgentValues.Include;

        foreach (var filter in filters)
        {
            var rule = new Rule(
                NameMatcher.Create(filter.Matcher, filter.Pattern, filter.CaseSensitive ?? caseSensitive),
                filter.ScopePath);

            if (filter.AppliesTo == AgentValues.Folder)
            {
                if (filter.Action == AgentValues.Exclude) _folderExcludes.Add(rule);
            }
            else (filter.Action == AgentValues.Exclude ? _fileExcludes : _fileIncludes).Add(rule);
        }
    }

    /// <summary>
    /// Whether the folder is walked and cataloged: it is selected, or an include mark lies below it (so
    /// the way down to it is kept) — and no folder filter excludes it or a folder above it.
    /// </summary>
    public bool IncludesFolder(string path) =>
        !IsFolderFiltered(path)
        && (IsSelected(path) || _marks.Any(m => m.Value && CatalogPath.IsUnder(m.Key, path, _comparison)));

    public bool IncludesFile(string path)
    {
        if (!IsSelected(path) || IsFolderFiltered(CatalogPath.Parent(path))) return false;

        var name = CatalogPath.Name(path);
        if (_fileExcludes.Any(r => InScope(r, path) && r.Matches(name, path))) return false;

        var includes = _fileIncludes.Where(r => InScope(r, path)).ToList();
        return includes.Count == 0 || includes.Any(r => r.Matches(name, path));
    }

    public bool Includes(string path, string kind) =>
        kind == AgentValues.Directory ? IncludesFolder(path) : IncludesFile(path);

    private bool IsSelected(string path)
    {
        for (var p = path; ; p = CatalogPath.Parent(p))
        {
            if (_marks.TryGetValue(p, out var include)) return include;
            if (p == CatalogPath.Root) return true;
        }
    }

    private bool IsFolderFiltered(string path)
    {
        for (var p = path; p != CatalogPath.Root; p = CatalogPath.Parent(p))
        {
            var name = CatalogPath.Name(p);
            if (_folderExcludes.Any(r => InScope(r, p) && r.Matches(name, p))) return true;
        }
        return false;
    }

    private bool InScope(Rule rule, string path) =>
        rule.ScopePath is null || CatalogPath.IsUnder(path, rule.ScopePath, _comparison);
}
