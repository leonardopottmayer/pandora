using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Application.Services;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPageGraph;

/// <summary>
/// The wiki graph as nodes and edges, already materialized in <c>PageLink</c> — this only shapes it.
/// The user's pages and their edges are loaded whole (a personal notebook is small) and the
/// neighborhood is cut in memory, which keeps the depth walk out of SQL.
///
/// Archived pages are nodes like any other: archiving hides a page from the sidebar, it does not
/// unlink it. Edges left dangling by a deleted page are dropped, same as in the backlinks panel.
/// </summary>
public sealed class GetPageGraphQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetPageGraphQuery, PageGraphDto>
{
    /// <summary>Past this, the local graph is the global one with extra steps.</summary>
    public const int MaxDepth = 5;

    protected override async Task<Result<PageGraphDto>> HandleAsync(
        GetPageGraphQuery request, CancellationToken ct)
    {
        var input = request.Input;
        var depth = Math.Clamp(input.Depth, 1, MaxDepth);

        var graph = await factory.ExecuteAsync(NotesModule.Name, async (ctx, token) =>
        {
            var pages = await ctx.AcquireRepository<IPageRepository>()
                .GetTreeForUserAsync(input.UserId, includeArchived: true, token);
            var byId = pages.ToDictionary(p => p.Id);

            // The root must exist and be the user's (404-on-foreign-resource rule).
            if (input.RootPageId is { } rootId && !byId.ContainsKey(rootId))
                return null;

            // The tag cut comes before the neighborhood walk: with a filter on, depth is counted over
            // the pages that survived it, not over the ones that were about to be hidden.
            var tagged = await TagFilter.MatchingPageIdsAsync(
                input.TagIds, ctx.AcquireRepository<IPageTagRepository>(), token);
            if (tagged is not null)
            {
                // The root itself may be filtered out — then there is no local graph to draw.
                if (input.RootPageId is { } filteredRoot && !tagged.Contains(filteredRoot))
                    return new PageGraphDto([], []);

                byId = byId.Where(p => tagged.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);
            }

            var links = await ctx.AcquireRepository<IPageLinkRepository>()
                .GetBySourcesAsync([.. byId.Keys], token);

            // An edge whose target no longer resolves — deleted, or cut by the tag filter — is
            // broken, not a node: drop it.
            var edges = links.Where(l => byId.ContainsKey(l.TargetPageId)).ToList();

            if (input.RootPageId is { } root)
            {
                var neighborhood = PageGraph.Neighborhood(
                    root, [.. edges.Select(e => (e.SourcePageId, e.TargetPageId))], depth);

                byId = byId.Where(p => neighborhood.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);
                edges = [.. edges.Where(e =>
                    neighborhood.Contains(e.SourcePageId) && neighborhood.Contains(e.TargetPageId))];
            }

            var degrees = byId.Keys.ToDictionary(id => id, _ => 0);
            foreach (var edge in edges)
            {
                degrees[edge.SourcePageId]++;
                degrees[edge.TargetPageId]++;
            }

            IReadOnlyList<GraphNodeDto> nodes =
            [
                .. byId.Values
                    .OrderBy(p => p.Title)
                    .Select(p => new GraphNodeDto(
                        p.Id, p.Title, p.Slug, p.Icon, p.IsArchived, degrees[p.Id]))
            ];

            IReadOnlyList<GraphEdgeDto> edgeDtos =
            [
                .. edges.Select(e => new GraphEdgeDto(e.SourcePageId, e.TargetPageId, e.Kind.Value))
            ];

            return new PageGraphDto(nodes, edgeDtos);
        }, cancellationToken: ct);

        return graph is null ? Fail(PageErrors.NotFound) : Ok(graph);
    }
}
