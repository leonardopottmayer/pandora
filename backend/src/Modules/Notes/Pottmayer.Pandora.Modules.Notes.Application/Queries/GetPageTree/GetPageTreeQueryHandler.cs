using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPageTree;

public sealed class GetPageTreeQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetPageTreeQuery, IReadOnlyList<PageSummaryDto>>
{
    protected override async Task<Result<IReadOnlyList<PageSummaryDto>>> HandleAsync(
        GetPageTreeQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var pages = await factory.ExecuteAsync(NotesModule.Name, async (ctx, token) =>
            await ctx.AcquireRepository<IPageRepository>()
                .GetTreeForUserAsync(input.UserId, input.IncludeArchived, token),
            cancellationToken: ct);

        IReadOnlyList<PageSummaryDto> dtos = [.. pages.Select(PageSummaryDto.From)];
        return Ok(dtos);
    }
}
