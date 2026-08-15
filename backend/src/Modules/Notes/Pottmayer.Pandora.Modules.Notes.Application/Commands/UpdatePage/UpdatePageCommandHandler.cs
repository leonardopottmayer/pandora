using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Application.Services;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.UpdatePage;

public sealed class UpdatePageCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<UpdatePageCommand, PageDto>
{
    protected override async Task<Result<PageDto>> HandleAsync(UpdatePageCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (string.IsNullOrWhiteSpace(input.Title))
            return Fail(PageErrors.InvalidTitle);

        var result = await factory.ExecuteAsync(NotesModule.Name, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IPageRepository>();

            var page = await repo.FindByIdForUserAsync(input.PageId, input.UserId, token);
            if (page is null)
                return Result<PageDto>.Failure([PageErrors.NotFound]);

            page.Update(input.Title, input.Icon, input.ContentMarkdown);
            await repo.UpdateAsync(page, token);

            // The save is where the wiki graph is materialized: rewrite this page's outgoing edges.
            var links = ctx.AcquireRepository<IPageLinkRepository>();
            await PageLinkSynchronizer.RebuildAsync(page, repo, links, timeProvider, token);

            // Same for the tags the body mentions — derived from the text, rewritten on every save.
            var tags = await PageTagSynchronizer.RebuildAsync(
                page,
                ctx.AcquireRepository<ITagRepository>(),
                ctx.AcquireRepository<IPageTagRepository>(),
                timeProvider,
                token);

            return Result<PageDto>.Success(PageDto.From(page, tags));
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
