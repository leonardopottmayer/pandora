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

        var dto = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IPageRepository>();
            var page = await repo.FindByIdForUserAsync(input.PageId, input.UserId, token);
            if (page is null)
                return null;

            // The tags the page's markdown carries, resolved through the rows the last save wrote.
            var pageTags = await ctx.AcquireRepository<IPageTagRepository>().GetByPageAsync(page.Id, token);
            var tags = await ctx.AcquireRepository<ITagRepository>()
                .GetByIdsForUserAsync([.. pageTags.Select(t => t.TagId)], input.UserId, token);

            return PageDto.From(page, tags);
        }, cancellationToken: ct);

        return dto is null ? Fail(PageErrors.NotFound) : Ok(dto);
    }
}
