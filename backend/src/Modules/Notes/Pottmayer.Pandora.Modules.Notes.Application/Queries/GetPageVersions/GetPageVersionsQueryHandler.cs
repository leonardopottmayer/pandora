using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPageVersions;

public sealed class GetPageVersionsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetPageVersionsQuery, IReadOnlyList<PageVersionSummaryDto>>
{
    protected override async Task<Result<IReadOnlyList<PageVersionSummaryDto>>> HandleAsync(GetPageVersionsQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            if (await ctx.AcquireRepository<IPageRepository>().FindByIdForUserAsync(input.PageId, input.UserId, token) is null)
                return Result<IReadOnlyList<PageVersionSummaryDto>>.Failure([PageErrors.NotFound]);

            var versions = await ctx.AcquireRepository<IPageVersionRepository>().ListAsync(input.PageId, input.UserId, input.Before, token);
            return Result<IReadOnlyList<PageVersionSummaryDto>>.Success(
                [.. versions.Select(PageVersionSummaryDto.From)]);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
