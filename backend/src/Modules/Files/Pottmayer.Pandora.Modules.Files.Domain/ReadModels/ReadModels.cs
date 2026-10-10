using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Files.Domain.ReadModels;

public sealed record UnseenEntry(Guid Id, string RelativePath, EntryKind Kind);

public sealed record MovePair(Guid OldId, Guid NewId);

public sealed record CatalogItem(Guid Id, Guid RootId, string RelativePath, EntryKind Kind);

/// <summary>
/// One child of a folder in the review tree: everything waiting below it is counted, and when the child
/// is itself an entry waiting for review, its id and status come along.
/// </summary>
public sealed record ReviewNode(string Name, string Path, int Count, bool HasChildren, Guid? EntryId, string? Status, string? Kind);

public enum ReviewDecision
{
    Forget,
    Keep
}

/// <summary>
/// Terms all match (as fragments of the name, title, artist or album); the other criteria are optional.
/// <see cref="MinShortSide"/>/<see cref="MaxShortSide"/> bound the shorter side of a video or photo, so a
/// portrait phone video counts as the resolution it was filmed in.
/// </summary>
public sealed record EntrySearch(
    Guid UserId,
    IReadOnlyList<string> Terms,
    IReadOnlyCollection<Guid>? RootIds,
    FileCategory? Category,
    long? MinSize,
    long? MaxSize,
    DateTimeOffset? ModifiedFrom,
    DateTimeOffset? ModifiedTo,
    EntryStatus? Status,
    DateTime? TakenFrom = null,
    DateTime? TakenBefore = null,
    int? MinShortSide = null,
    int? MaxShortSide = null,
    int? MinDuration = null,
    int? MaxDuration = null);
