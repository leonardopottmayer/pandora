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

    /// <summary>Drops every edge leaving this page. Runs when the page is deleted.</summary>
    Task RemoveBySourceAsync(Guid sourcePageId, CancellationToken ct = default);
}
