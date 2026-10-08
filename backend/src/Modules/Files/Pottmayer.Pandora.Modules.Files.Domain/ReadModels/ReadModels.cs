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

/// <summary>Name terms all match (as fragments); the other criteria are optional.</summary>
public sealed record EntrySearch(
    Guid UserId,
    IReadOnlyList<string> Terms,
    IReadOnlyCollection<Guid>? RootIds,
    FileCategory? Category,
    long? MinSize,
    long? MaxSize,
    DateTimeOffset? ModifiedFrom,
    DateTimeOffset? ModifiedTo,
    EntryStatus? Status);
