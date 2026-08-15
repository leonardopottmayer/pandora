using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetTags;

/// <summary>
/// Every tag of the user, with the count of live pages carrying it. The count is done over the
/// user's pages read whole (a personal notebook is small — the same trade the graph makes), which
/// is also what keeps soft-deleted pages out of it for free.
/// </summary>
public sealed class GetTagsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetTagsQuery, IReadOnlyList<TagDto>>
{
    protected override async Task<Result<IReadOnlyList<TagDto>>> HandleAsync(
        GetTagsQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var dtos = await factory.ExecuteAsync(NotesModule.Name, async (ctx, token) =>
        {
            var tags = await ctx.AcquireRepository<ITagRepository>().GetForUserAsync(input.UserId, token);
            if (tags.Count == 0)
                return (IReadOnlyList<TagDto>)[];

            var pages = await ctx.AcquireRepository<IPageRepository>()
                .GetTreeForUserAsync(input.UserId, includeArchived: true, token);

            var counts = (await ctx.AcquireRepository<IPageTagRepository>()
                    .GetByPagesAsync([.. pages.Select(p => p.Id)], token))
                .GroupBy(t => t.TagId)
                .ToDictionary(g => g.Key, g => g.Count());

            return (IReadOnlyList<TagDto>)
            [
                .. tags.Select(t => TagDto.From(t, counts.GetValueOrDefault(t.Id)))
            ];
        }, cancellationToken: ct);

        return Ok(dtos);
    }
}
