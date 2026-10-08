namespace Pottmayer.Pandora.Modules.Files.Agent;

// The agent protocol (docs/modules/files/en/product-plan.md §4.4). Plain records so any .NET agent can
// share them; agents in other languages follow the same JSON.

/// <summary>Values of the string fields below.</summary>
public static class AgentValues
{
    public const string Include = "include";
    public const string Exclude = "exclude";

    /// <summary>A filter's <see cref="FilterConfig.AppliesTo"/>, and an entry's <see cref="ScannedEntry.Kind"/>.</summary>
    public const string File = "file";

    /// <summary>A filter's <see cref="FilterConfig.AppliesTo"/>.</summary>
    public const string Folder = "folder";

    /// <summary>An entry's <see cref="ScannedEntry.Kind"/>.</summary>
    public const string Directory = "directory";
}

/// <summary><c>GET /files/agent/config</c>: what this device scans. No roots while the account switch is off.</summary>
public sealed record AgentConfig(bool Enabled, IReadOnlyList<RootConfig> Roots);

/// <summary>
/// One root, with the selection marks and the enabled filters in scope for it.
/// <see cref="LastCompletedScanAt"/> lets the agent tell whether today's scheduled scan already ran.
/// </summary>
public sealed record RootConfig(
    Guid Id,
    string Name,
    string LocalPath,
    bool CaseSensitive,
    bool IncludeHidden,
    TimeOnly? ScanTime,
    DateTimeOffset? LastCompletedScanAt,
    IReadOnlyList<MarkConfig> Marks,
    IReadOnlyList<FilterConfig> Filters);

/// <summary>A selection mark: <see cref="Mode"/> is <c>include</c> or <c>exclude</c>; <c>/</c> is the root.</summary>
public sealed record MarkConfig(string Path, string Mode);

/// <summary>A filter as the engine needs it. <see cref="ScopePath"/> limits it to what lies below that folder.</summary>
public sealed record FilterConfig(
    string Action,
    string AppliesTo,
    string Matcher,
    string Pattern,
    bool? CaseSensitive,
    string? ScopePath);

public sealed record StartScanRequest(Guid RootId);

public sealed record StartScanResponse(Guid ScanId);

/// <summary>Up to <see cref="MaxEntries"/> entries; the catalog paths are relative to the root.</summary>
public sealed record ScanBatch(IReadOnlyList<ScannedEntry> Entries)
{
    public const int MaxEntries = 1000;
}

/// <summary>
/// One item the walk let through. <see cref="Fingerprint"/> is sent only when the backend asked for it
/// (see <see cref="Agent.Fingerprint"/>); folders never carry one, nor a size or date.
/// </summary>
public sealed record ScannedEntry(string Path, string Kind, long Size, DateTimeOffset? ModifiedAt, string? Fingerprint);

/// <summary>The files of the batch the backend has no fingerprint for: send them again with one.</summary>
public sealed record ScanBatchResult(IReadOnlyList<string> NeedsFingerprint);

/// <summary><see cref="EntriesSeen"/> counts distinct paths sent; a mismatch aborts the scan instead of marking files missing.</summary>
public sealed record CompleteScanRequest(int EntriesSeen);

/// <summary>Why the agent gave up, e.g. <c>root-unavailable</c>.</summary>
public sealed record AbortScanRequest(string Reason);
