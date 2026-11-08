using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Application.Services;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.Trash;

// The trash: deleted pages wait there to be brought back or deleted for good. A page comes back with
// the sub-pages deleted together with it (same moment); one deleted on its own earlier stays.

internal static class TrashTree
{
    /// <summary>The pages deleted together with <paramref name="root"/>: its deleted sub-pages of the same moment, root first.</summary>
    public static List<Page> DeletedWith(Page root, IReadOnlyList<Page> deleted)
    {
        var byParent = deleted.Where(p => p.ParentId is not null).ToLookup(p => p.ParentId!.Value);
        var found = new List<Page> { root };
        for (var i = 0; i < found.Count; i++)
            found.AddRange(byParent[found[i].Id].Where(p => p.DeletedAt == root.DeletedAt));
        return found;
    }

    /// <summary>Every deleted sub-page below <paramref name="root"/>, whenever it was deleted, root first.</summary>
    public static List<Page> AllBelow(Page root, IReadOnlyList<Page> deleted)
    {
        var byParent = deleted.Where(p => p.ParentId is not null).ToLookup(p => p.ParentId!.Value);
        var found = new List<Page> { root };
        for (var i = 0; i < found.Count; i++)
            found.AddRange(byParent[found[i].Id]);
        return found;
    }

    /// <summary>Removes pages for good, leaves first, with all that pointed at them.</summary>
    public static async Task PurgeAsync(IDataContext ctx, IReadOnlyList<Page> pages, Guid userId, CancellationToken ct)
    {
        var repo = ctx.AcquireRepository<IPageRepository>();
        var links = ctx.AcquireRepository<IPageLinkRepository>();
        var versions = ctx.AcquireRepository<IPageVersionRepository>();
        foreach (var page in Enumerable.Reverse(pages))
        {
            await links.PurgeByPageAsync(page.Id, ct);
            await versions.PurgeByPageAsync(page.Id, ct);
            await ctx.AcquireRepository<IPageCommentRepository>().PurgeByPageAsync(page.Id, ct);
            await ctx.AcquireRepository<IBlockStampRepository>().PurgeByPageAsync(page.Id, ct);
            await repo.PurgeAsync(page.Id, userId, ct);
        }
    }
}

// ---------- List ----------

public sealed record GetTrashInput(Guid UserId);

/// <summary>What is in the trash: each page deleted on its own, with how many sub-pages went with it.</summary>
[RequiresPermission(NotesPermissions.Pages.Read)]
public sealed class GetTrashQuery(GetTrashInput input) : QueryBase<GetTrashInput, IReadOnlyList<TrashItemDto>>(input);

public sealed class GetTrashQueryHandler(IUnitOfWorkFactory factory) : QueryHandlerBase<GetTrashQuery, IReadOnlyList<TrashItemDto>>
{
    protected override async Task<Result<IReadOnlyList<TrashItemDto>>> HandleAsync(GetTrashQuery request, CancellationToken ct)
    {
        var items = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var deleted = await ctx.AcquireRepository<IPageRepository>().GetDeletedForUserAsync(request.Input.UserId, token);
            var byId = deleted.ToDictionary(p => p.Id);
            // A page whose parent went in the same deletion is listed under it, not on its own.
            IReadOnlyList<TrashItemDto> list =
            [
                .. deleted
                    .Where(p => p.ParentId is not { } parent || !byId.TryGetValue(parent, out var above) || above.DeletedAt != p.DeletedAt)
                    .Select(p => new TrashItemDto(p.Id, p.SpaceId, p.Title, p.Icon, p.DeletedAt!.Value, TrashTree.DeletedWith(p, deleted).Count - 1)),
            ];
            return list;
        }, cancellationToken: ct);
        return Ok(items);
    }
}

// ---------- Restore ----------

public sealed record RestoreFromTrashInput(Guid UserId, Guid PageId);

/// <summary>
/// Brings a page back with the sub-pages deleted with it. It returns under its parent, or to the top
/// of its space (in its folder) when the parent is gone; to the default space when its space is gone.
/// </summary>
[RequiresPermission(NotesPermissions.Pages.Delete)]
public sealed class RestoreFromTrashCommand(RestoreFromTrashInput input) : CommandBase<RestoreFromTrashInput, PageDto>(input);

public sealed class RestoreFromTrashCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<RestoreFromTrashCommand, PageDto>
{
    protected override async Task<Result<PageDto>> HandleAsync(RestoreFromTrashCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IPageRepository>();
            var deleted = await repo.GetDeletedForUserAsync(input.UserId, token);
            var root = deleted.FirstOrDefault(p => p.Id == input.PageId);
            if (root is null)
                return Result<PageDto>.Failure([PageErrors.NotInTrash]);

            var parent = root.ParentId is { } parentId ? await repo.FindByIdForUserAsync(parentId, input.UserId, token) : null;
            var space = parent is null ? await ctx.AcquireRepository<ISpaceRepository>().FindByIdForUserAsync(root.SpaceId, input.UserId, token) : null;
            if (parent is null && space is null)
            {
                var fallback = await SpaceResolver.ResolveAsync(ctx.AcquireRepository<ISpaceRepository>(), input.UserId, null, timeProvider, token);
                space = fallback.Value!;
            }
            var spaceId = parent?.SpaceId ?? space!.Id;
            var folder = parent is null && root.FolderId is { } folderId && space!.Id == root.SpaceId
                ? await ctx.AcquireRepository<IFolderRepository>().FindByIdForUserAsync(folderId, input.UserId, token)
                : null;

            Tag[] rootTags = [];
            foreach (var page in TrashTree.DeletedWith(root, deleted))
            {
                var isRoot = page.Id == root.Id;
                var slug = await FreeSlugAsync(repo, input.UserId, page.Slug, token);
                page.RestoreFromTrash(spaceId, isRoot ? parent?.Id : page.ParentId, isRoot ? folder?.Id : null, slug);
                await repo.UpdateAsync(page, token);

                // What the page derived went away with it: work it out again from its blocks.
                var content = PageContent.Parse(page.Content);
                await PageLinkSynchronizer.RebuildAsync(page, content, repo, ctx.AcquireRepository<IPageLinkRepository>(), timeProvider, token);
                await PageMediaSynchronizer.RebuildAsync(
                    page, content, ctx.AcquireRepository<IMediaItemRepository>(), ctx.AcquireRepository<IPageMediaRepository>(), timeProvider, token);
                var tags = await PageTagSynchronizer.RebuildAsync(
                    page, content, ctx.AcquireRepository<ITagRepository>(), ctx.AcquireRepository<IPageTagRepository>(), timeProvider, token);
                if (isRoot) rootTags = [.. tags];
            }
            return Result<PageDto>.Success(PageDto.From(root, rootTags));
        }, cancellationToken: ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }

    /// <summary>The page's own slug, or the same with a number when a live page took it meanwhile.</summary>
    private static async Task<string> FreeSlugAsync(IPageRepository repo, Guid userId, string slug, CancellationToken ct)
    {
        if (!await repo.ExistsWithSlugAsync(userId, slug, ct))
            return slug;
        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{slug}-{suffix}";
            if (!await repo.ExistsWithSlugAsync(userId, candidate, ct))
                return candidate;
        }
    }
}

// ---------- Delete for good ----------

public sealed record DeleteFromTrashInput(Guid UserId, Guid? PageId);

/// <summary>Deletes a page in the trash for good, with every deleted page below it; with no page, empties the trash.</summary>
[RequiresPermission(NotesPermissions.Pages.Delete)]
public sealed class DeleteFromTrashCommand(DeleteFromTrashInput input) : CommandBase<DeleteFromTrashInput, int>(input);

public sealed class DeleteFromTrashCommandHandler(IUnitOfWorkFactory factory) : CommandHandlerBase<DeleteFromTrashCommand, int>
{
    protected override async Task<Result<int>> HandleAsync(DeleteFromTrashCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var deleted = await ctx.AcquireRepository<IPageRepository>().GetDeletedForUserAsync(input.UserId, token);
            List<Page> doomed;
            if (input.PageId is { } id)
            {
                var root = deleted.FirstOrDefault(p => p.Id == id);
                if (root is null)
                    return Result<int>.Failure([PageErrors.NotInTrash]);
                doomed = TrashTree.AllBelow(root, deleted);
            }
            else
            {
                // Parents before children in the list; PurgeAsync walks it backwards.
                var byId = deleted.ToDictionary(p => p.Id);
                doomed = [.. deleted.Where(p => p.ParentId is not { } parent || !byId.ContainsKey(parent))
                    .SelectMany(p => TrashTree.AllBelow(p, deleted)).DistinctBy(p => p.Id)];
            }
            await TrashTree.PurgeAsync(ctx, doomed, input.UserId, token);
            return Result<int>.Success(doomed.Count);
        }, cancellationToken: ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value);
    }
}
