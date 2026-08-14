using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;

public interface IPageRepository : IStandardRepository<Page, Guid>
{
    /// <summary>One non-deleted page owned by the user, or <c>null</c> (404-on-foreign-resource rule).</summary>
    Task<Page?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// The user's non-deleted pages for the sidebar tree, ordered by parent then <see cref="Page.OrderIndex"/>;
    /// archived ones are optional.
    /// </summary>
    Task<IReadOnlyList<Page>> GetTreeForUserAsync(
        Guid userId, bool includeArchived, CancellationToken ct = default);

    /// <summary>
    /// Maps every non-deleted page id to its parent id for the user (roots map to <c>null</c>). Feeds the
    /// cycle check on reparent — archived pages are included, since they still occupy the tree.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Guid?>> GetParentMapForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Whether the user already has a non-deleted page with this slug.</summary>
    Task<bool> ExistsWithSlugAsync(Guid userId, string slug, CancellationToken ct = default);

    /// <summary>
    /// Candidate wikilink targets: the user's non-deleted pages whose title (case-insensitive) is in
    /// <paramref name="lowerTitles"/> or whose slug is in <paramref name="slugs"/>. Archived pages
    /// count — archiving hides a page from the sidebar, it does not unlink it.
    /// </summary>
    Task<IReadOnlyList<Page>> FindByTitlesOrSlugsAsync(
        Guid userId,
        IReadOnlyCollection<string> lowerTitles,
        IReadOnlyCollection<string> slugs,
        CancellationToken ct = default);

    /// <summary>The user's non-deleted pages among <paramref name="ids"/>, ordered by title.</summary>
    Task<IReadOnlyList<Page>> GetByIdsForUserAsync(
        IReadOnlyCollection<Guid> ids, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// The user's non-deleted pages whose title or body match <paramref name="tsQuery"/> (built by
    /// <see cref="PageSearch.ToTsQuery"/>), ordered by title and capped at <paramref name="limit"/>.
    /// Archived pages count — they are hidden from the sidebar, not from search.
    /// </summary>
    Task<IReadOnlyList<Page>> SearchAsync(
        Guid userId, string tsQuery, int limit, CancellationToken ct = default);
}
