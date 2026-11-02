using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;

public interface IMediaItemRepository : IStandardRepository<MediaItem, Guid>
{
    /// <summary>One non-deleted item owned by the user, or <c>null</c> (404-on-foreign-resource rule).</summary>
    Task<MediaItem?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// The user's non-deleted items, newest first: one folder's own items, or the unsorted ones, or —
    /// with neither — every one. <paramref name="kind"/> and a name fragment
    /// (<paramref name="search"/>, case-insensitive) narrow further.
    /// </summary>
    Task<IReadOnlyList<MediaItem>> ListAsync(
        Guid userId, Guid? folderId, bool unsortedOnly, string? kind, string? search, CancellationToken ct = default);

    /// <summary>How many non-deleted items each folder holds directly, and how many have no folder.</summary>
    Task<MediaCounts> CountAsync(Guid userId, CancellationToken ct = default);

    /// <summary>The user's non-deleted items directly in <paramref name="folderId"/>.</summary>
    Task<IReadOnlyList<MediaItem>> GetInFolderAsync(Guid userId, Guid folderId, CancellationToken ct = default);
}

public sealed record MediaCounts(IReadOnlyDictionary<Guid, int> ByFolder, int Unsorted, int Total);

public interface IMediaFolderRepository : IStandardRepository<MediaFolder, Guid>
{
    Task<MediaFolder?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>Every folder of the user, by name.</summary>
    Task<IReadOnlyList<MediaFolder>> GetForUserAsync(Guid userId, CancellationToken ct = default);
}

public interface IMediaRenditionRepository : IStandardRepository<MediaRendition, Guid>
{
    Task<MediaRendition?> FindAsync(Guid mediaId, string purpose, CancellationToken ct = default);

    /// <summary>Which of <paramref name="mediaIds"/> have a rendition for <paramref name="purpose"/>.</summary>
    Task<IReadOnlySet<Guid>> WithPurposeAsync(
        IReadOnlyCollection<Guid> mediaIds, string purpose, CancellationToken ct = default);
}
