using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;

/// <summary>
/// Edges are addressed by their endpoints, never by their own id. Owner scoping lives on the pages:
/// an edge only exists between two pages of the same user, so checking the endpoint page is enough.
/// </summary>
public interface IPageLinkRepository : IStandardRepository<PageLink, Guid>
{
    /// <summary>Edges leaving this page — the set a save rewrites.</summary>
    Task<IReadOnlyList<PageLink>> GetBySourceAsync(Guid sourcePageId, CancellationToken ct = default);

    /// <summary>Edges pointing at this page — the backlinks ("linked mentions") panel.</summary>
    Task<IReadOnlyList<PageLink>> GetByTargetAsync(Guid targetPageId, CancellationToken ct = default);

    /// <summary>
    /// Every edge leaving any of these pages — the whole graph of one user in a single read, given the
    /// user's page ids. Feeds the graph view, which needs the edges before it can pick a neighborhood.
    /// </summary>
    Task<IReadOnlyList<PageLink>> GetBySourcesAsync(
        IReadOnlyCollection<Guid> sourcePageIds, CancellationToken ct = default);

    /// <summary>Drops every edge leaving this page. Runs when the page is deleted.</summary>
    Task RemoveBySourceAsync(Guid sourcePageId, CancellationToken ct = default);
}
