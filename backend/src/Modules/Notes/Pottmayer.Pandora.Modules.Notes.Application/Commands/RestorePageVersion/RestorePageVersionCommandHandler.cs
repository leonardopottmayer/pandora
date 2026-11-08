using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Application.Services;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.RestorePageVersion;

public sealed class RestorePageVersionCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<RestorePageVersionCommand, PageDto>
{
    protected override async Task<Result<PageDto>> HandleAsync(RestorePageVersionCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var pages = ctx.AcquireRepository<IPageRepository>();
            var page = await pages.FindByIdForUserAsync(input.PageId, input.UserId, token);
            if (page is null)
                return Result<PageDto>.Failure([PageErrors.NotFound]);

            if (page.HistoryNumber != input.ExpectedVersion)
                return Result<PageDto>.Failure([PageErrors.HistoryConflict]);

            var version = await ctx.AcquireRepository<IPageVersionRepository>().FindAsync(page.Id, input.UserId, input.Number, token);
            if (version is null)
                return Result<PageDto>.Failure([PageErrors.NotFound]);

            var snapshot = version.Snapshot;
            if (snapshot.DeletedAt is not null)
                return Result<PageDto>.Failure([PageErrors.InvalidRestore]);

            if (await ctx.AcquireRepository<ISpaceRepository>().FindByIdForUserAsync(snapshot.SpaceId, input.UserId, token) is null)
                return Result<PageDto>.Failure([SpaceErrors.NotFound]);

            if (snapshot.ParentId is { } parentId)
            {
                var parent = await pages.FindByIdForUserAsync(parentId, input.UserId, token);
                if (parent is null)
                    return Result<PageDto>.Failure([PageErrors.ParentNotFound]);

                if (parent.SpaceId != snapshot.SpaceId)
                    return Result<PageDto>.Failure([PageErrors.InvalidRestore]);

                var parents = await pages.GetParentMapForUserAsync(input.UserId, token);
                if (PageHierarchy.WouldCreateCycle(page.Id, parentId, parents))
                    return Result<PageDto>.Failure([PageErrors.CycleDetected]);
            }

            if (snapshot.SpaceId != page.SpaceId)
            {
                var all = await pages.GetTreeForUserAsync(input.UserId, includeArchived: true, token);
                foreach (var child in PageHierarchy.Subtree(page.Id, all).Where(p => p.Id != page.Id))
                {
                    child.MoveToSpace(snapshot.SpaceId);
                    await pages.UpdateAsync(child, token);
                }
            }

            page.Restore(snapshot, version.Number);
            if (page.FolderId is { } folderId &&
                (await ctx.AcquireRepository<IFolderRepository>().FindByIdForUserAsync(folderId, input.UserId, token))?.SpaceId != page.SpaceId)
                page.LeaveFolder();
            var content = PageContent.Parse(page.Content);
            await pages.UpdateAsync(page, token);

            await PageLinkSynchronizer.RebuildAsync(page, content, pages, ctx.AcquireRepository<IPageLinkRepository>(), timeProvider, token);

            await PageMediaSynchronizer.RebuildAsync(
                page, content, ctx.AcquireRepository<IMediaItemRepository>(),
                ctx.AcquireRepository<IPageMediaRepository>(), timeProvider, token);

            // And when each block was written, and by what.
            await BlockStampSynchronizer.RebuildAsync(page, content, ctx.AcquireRepository<IBlockStampRepository>(), timeProvider, token);

            var tags = await PageTagSynchronizer.RebuildAsync(
                page,
                content,
                ctx.AcquireRepository<ITagRepository>(),
                ctx.AcquireRepository<IPageTagRepository>(),
                timeProvider,
                token);

            return Result<PageDto>.Success(PageDto.From(page, tags));
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
