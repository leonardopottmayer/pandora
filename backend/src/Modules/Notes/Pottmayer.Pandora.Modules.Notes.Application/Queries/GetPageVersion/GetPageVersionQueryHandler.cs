using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPageVersion;

public sealed class GetPageVersionQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetPageVersionQuery, PageVersionDto>
{
    protected override async Task<Result<PageVersionDto>> HandleAsync(GetPageVersionQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var dto = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            if (await ctx.AcquireRepository<IPageRepository>().FindByIdForUserAsync(input.PageId, input.UserId, token) is null)
                return null;

            var version = await ctx.AcquireRepository<IPageVersionRepository>().FindAsync(input.PageId, input.UserId, input.Number, token);
            return version is null ? null : PageVersionDto.From(version);
        }, cancellationToken: ct);

        return dto is null ? Fail(PageErrors.NotFound) : Ok(dto);
    }
}
