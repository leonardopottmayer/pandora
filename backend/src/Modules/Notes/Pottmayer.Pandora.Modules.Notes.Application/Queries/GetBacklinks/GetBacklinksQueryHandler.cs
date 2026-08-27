using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetBacklinks;

/// <summary>
/// Who points at this page. Edges left behind by a deleted source page are dropped on read, which is
/// what keeps a "broken" edge from surfacing as a phantom mention.
/// </summary>
public sealed class GetBacklinksQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetBacklinksQuery, IReadOnlyList<BacklinkDto>>
{
    protected override async Task<Result<IReadOnlyList<BacklinkDto>>> HandleAsync(
        GetBacklinksQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var backlinks = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var pages = ctx.AcquireRepository<IPageRepository>();

            // The page itself must exist and be the user's (404-on-foreign-resource rule).
            if (await pages.FindByIdForUserAsync(input.PageId, input.UserId, token) is null)
                return null;

            var links = ctx.AcquireRepository<IPageLinkRepository>();
            var edges = await links.GetByTargetAsync(input.PageId, token);
            if (edges.Count == 0)
                return (IReadOnlyList<BacklinkDto>)[];

            var sources = await pages.GetByIdsForUserAsync(
                [.. edges.Select(e => e.SourcePageId).Distinct()], input.UserId, token);
            var byId = sources.ToDictionary(p => p.Id);

            return (IReadOnlyList<BacklinkDto>)
            [
                .. edges
                    .Where(e => byId.ContainsKey(e.SourcePageId))
                    .Select(e =>
                    {
                        var source = byId[e.SourcePageId];
                        return new BacklinkDto(
                            source.Id, source.Title, source.Slug, source.Icon, source.IsArchived, e.Kind.Value);
                    })
                    .OrderBy(b => b.Title)
                    .ThenBy(b => b.Kind)
            ];
        }, cancellationToken: ct);

        return backlinks is null ? Fail(PageErrors.NotFound) : Ok(backlinks);
    }
}
