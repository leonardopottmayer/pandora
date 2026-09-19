using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;

/// <summary>
/// Pageâ†”tag rows are addressed by their endpoints, never by their own id. Owner scoping lives on the
/// page and on the tag, which always belong to the same user.
/// </summary>
public interface IPageTagRepository : IStandardRepository<PageTag, Guid>
{
    /// <summary>The tags of one page â€” the set a save rewrites.</summary>
    Task<IReadOnlyList<PageTag>> GetByPageAsync(Guid pageId, CancellationToken ct = default);

    /// <summary>
    /// Every row of these pages in a single read. Given the user's live page ids, this is the whole
    /// tagging of one notebook â€” enough to count usage or to intersect a filter in memory.
    /// </summary>
    Task<IReadOnlyList<PageTag>> GetByPagesAsync(
        IReadOnlyCollection<Guid> pageIds, CancellationToken ct = default);

    /// <summary>Every row carrying one of these tags â€” the filter's side of the same question.</summary>
    Task<IReadOnlyList<PageTag>> GetByTagsAsync(
        IReadOnlyCollection<Guid> tagIds, CancellationToken ct = default);

    /// <summary>Drops every tag of this page. Runs when the page is deleted.</summary>
    Task RemoveByPageAsync(Guid pageId, CancellationToken ct = default);
}
