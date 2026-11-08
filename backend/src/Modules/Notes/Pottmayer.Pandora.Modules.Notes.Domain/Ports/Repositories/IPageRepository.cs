using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;

public interface IPageRepository : IStandardRepository<Page, Guid>
{
    /// <summary>One non-deleted page owned by the user, or <c>null</c> (404-on-foreign-resource rule).</summary>
    Task<Page?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>Only direct, nondeleted pages of an owned folder, optionally narrowed by archive state and ids.</summary>
    Task<IReadOnlyList<Page>> GetInFolderAsync(Guid userId, Guid folderId, bool includeArchived,
        IReadOnlyCollection<Guid>? pageIds = null, CancellationToken ct = default);

    /// <summary>
    /// The user's non-deleted pages for the sidebar tree, ordered by parent then <see cref="Page.OrderIndex"/>;
    /// archived ones are optional.
    /// </summary>
    Task<IReadOnlyList<Page>> GetTreeForUserAsync(
        Guid userId, bool includeArchived, CancellationToken ct = default);

    /// <summary>
    /// Maps every non-deleted page id to its parent id for the user (roots map to <c>null</c>). Feeds the
    /// cycle check on reparent â€” archived pages are included, since they still occupy the tree.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Guid?>> GetParentMapForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Whether the user already has a non-deleted page with this slug.</summary>
    Task<bool> ExistsWithSlugAsync(Guid userId, string slug, CancellationToken ct = default);

    /// <summary>The user's non-deleted pages among <paramref name="ids"/>, ordered by title.</summary>
    Task<IReadOnlyList<Page>> GetByIdsForUserAsync(
        IReadOnlyCollection<Guid> ids, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// The user's non-deleted pages whose title or body match <paramref name="tsQuery"/> (built by
    /// <see cref="PageSearch.ToTsQuery"/>), ordered by title match, relevance, title and id,
    /// capped at <paramref name="limit"/> after an optional <paramref name="pageIds"/> filter.
    /// Archived pages count â€” they are hidden from the sidebar, not from search. A
    /// <paramref name="spaceId"/> keeps the hits to that space.
    /// </summary>
    Task<IReadOnlyList<Page>> SearchAsync(
        Guid userId, string tsQuery, int limit, Guid? spaceId = null,
        IReadOnlyCollection<Guid>? pageIds = null, CancellationToken ct = default);

    /// <summary>
    /// Per space of the user: how many non-deleted pages it holds (archived included) and when one of
    /// them last changed. Spaces with no page are absent.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, SpacePageStats>> GetSpaceStatsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>The user's non-deleted pages opened most recently, newest first.</summary>
    Task<IReadOnlyList<Page>> GetRecentForUserAsync(Guid userId, int limit, CancellationToken ct = default);

    /// <summary>
    /// Stamps when the page was opened, straight in the store: a visit is not an edit, so neither the
    /// page's "edited" time nor its versions move. False when it is not the user's live page.
    /// </summary>
    Task<bool> MarkViewedAsync(Guid id, Guid userId, DateTimeOffset at, CancellationToken ct = default);

    /// <summary>The user's soft-deleted pages (the trash), most recently deleted first.</summary>
    Task<IReadOnlyList<Page>> GetDeletedForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a soft-deleted page's row for good, straight in the store. Its links, versions, tags and
    /// media uses must be gone first, and its sub-pages before it.
    /// </summary>
    Task PurgeAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>The non-deleted page whose public link is <paramref name="token"/>, whoever owns it.</summary>
    Task<Page?> FindByPublicTokenAsync(string token, CancellationToken ct = default);
}

public sealed record SpacePageStats(int PageCount, DateTimeOffset LastEditedAt);
