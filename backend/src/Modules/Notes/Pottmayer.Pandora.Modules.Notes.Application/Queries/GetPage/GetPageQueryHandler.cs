using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPage;

public sealed class GetPageQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetPageQuery, PageDto>
{
    protected override async Task<Result<PageDto>> HandleAsync(GetPageQuery request, CancellationToken ct)
    {
        var input = request.Input;

        var page = await factory.ExecuteAsync(NotesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IPageRepository>();
            return await repo.FindByIdForUserAsync(input.PageId, input.UserId, token);
        }, cancellationToken: ct);

        return page is null
            ? Fail(PageErrors.NotFound)
            : Ok(PageDto.From(page));
    }
}
