using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.ReadModels;

namespace Pottmayer.Pandora.Modules.Files.Application.Dtos;

public sealed record PreferencesDto(bool IsEnabled);

public sealed record RootDto(
    Guid Id,
    Guid DeviceId,
    string Name,
    string LocalPath,
    bool CaseSensitive,
    bool IncludeHidden,
    TimeOnly? ScanTime,
    string Status,
    DateTimeOffset? LastCompletedScanAt,
    int EntryCount,
    IReadOnlyList<MarkConfig> Marks)
{
    public static RootDto From(Root r) => new(
        r.Id, r.DeviceId, r.Name, r.LocalPath, r.CaseSensitive, r.IncludeHidden, r.ScanTime, r.Status.Value,
        r.LastCompletedScanAt, r.EntryCount,
        [.. r.Marks.OrderBy(m => m.Path, StringComparer.Ordinal).Select(m => new MarkConfig(m.Path, m.Mode.Value))]);
}

public sealed record FilterDto(
    Guid Id,
    Guid? DeviceId,
    Guid? RootId,
    string? ScopePath,
    string Name,
    string Action,
    string AppliesTo,
    string Matcher,
    string Pattern,
    bool? CaseSensitive,
    bool IsEnabled,
    bool IsBuiltin)
{
    public static FilterDto From(Filter f) => new(
        f.Id, f.DeviceId, f.RootId, f.ScopePath, f.Name, f.Action.Value, f.AppliesTo.Value, f.Matcher.Value,
        f.Pattern, f.CaseSensitive, f.IsEnabled, f.IsBuiltin);
}

public sealed record EntryDto(
    Guid Id,
    Guid RootId,
    string Kind,
    string RelativePath,
    string ParentPath,
    string Name,
    string? Extension,
    string? Category,
    long SizeBytes,
    DateTimeOffset? ModifiedAt,
    string Status,
    DateTimeOffset? MissingSince,
    DateTimeOffset? KeptAt,
    DateTimeOffset FirstSeenAt)
{
    public static EntryDto From(Entry e) => new(
        e.Id, e.RootId, e.Kind.Value, e.RelativePath, e.ParentPath, e.Name, e.Extension, e.Category?.Value,
        e.SizeBytes, e.ModifiedAt, e.Status.Value, e.MissingSince, e.KeptAt, e.FirstSeenAt);
}

/// <summary>A page without a total: counting millions of rows for a pager is not worth it.</summary>
public sealed record PageDto<T>(IReadOnlyList<T> Items, bool HasMore);

public sealed record ScanDto(
    Guid Id,
    Guid RootId,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? LastBatchAt,
    DateTimeOffset? FinishedAt,
    int Seen,
    int Created,
    int Changed,
    int Moved,
    int Missing,
    int Excluded,
    string? Error)
{
    public static ScanDto From(Scan s) => new(
        s.Id, s.RootId, s.Status.Value, s.StartedAt, s.LastBatchAt, s.FinishedAt,
        s.Seen, s.Created, s.Changed, s.Moved, s.Missing, s.Excluded, s.Error);
}

public sealed record FilterPreviewItem(Guid Id, Guid RootId, string RelativePath);

/// <summary>How many catalog entries a draft filter matches now, with a sample.</summary>
public sealed record FilterPreviewDto(int Count, IReadOnlyList<FilterPreviewItem> Sample);

/// <summary>
/// One node of the review tree. At the top level each node is a root (<see cref="Path"/> <c>/</c>); below,
/// a child folder or entry, counting everything waiting at or below it.
/// </summary>
public sealed record ReviewNodeDto(
    Guid RootId,
    string Name,
    string Path,
    int Count,
    bool HasChildren,
    Guid? EntryId,
    string? Status,
    string? Kind)
{
    public static ReviewNodeDto From(Guid rootId, ReviewNode n) =>
        new(rootId, n.Name, n.Path, n.Count, n.HasChildren, n.EntryId, n.Status, n.Kind);
}
