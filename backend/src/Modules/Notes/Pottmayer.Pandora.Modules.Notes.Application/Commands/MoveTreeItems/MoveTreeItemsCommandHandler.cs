using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.MoveTreeItems;

public sealed class MoveTreeItemsCommandHandler(IUnitOfWorkFactory factory, IPermissionChecker permissions)
    : CommandHandlerBase<MoveTreeItemsCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(MoveTreeItemsCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (input.Kind is not ("page" or "folder") || input.ItemIds.Count == 0 ||
            input.ItemIds.Distinct().Count() != input.ItemIds.Count ||
            (input.ParentId is not null && input.FolderId is not null) ||
            (input.Kind == "folder" && input.FolderId is not null))
            return Fail(TreeErrors.InvalidMove);
        var permission = input.Kind == "page" ? NotesPermissions.Pages.Update : NotesPermissions.Folders.Update;
        if (!await permissions.HasAsync(input.UserId.ToString(), permission, ct))
            return Fail(TreeErrors.MoveDenied);

        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            await using var userLock = await ctx.AcquireRepository<INotesPreferencesRepository>().LockForUserAsync(input.UserId, token);
            if (await ctx.AcquireRepository<ISpaceRepository>().FindByIdForUserAsync(input.SpaceId, input.UserId, token) is null)
                return Result<bool>.Failure([SpaceErrors.NotFound]);
            var pages = ctx.AcquireRepository<IPageRepository>();
            var folders = ctx.AcquireRepository<IFolderRepository>();
            var destinationFolderId = input.Kind == "folder" ? input.ParentId : input.FolderId;
            if (destinationFolderId is { } folderId)
            {
                var folder = await folders.FindByIdForUserAsync(folderId, input.UserId, token);
                if (folder is null || folder.SpaceId != input.SpaceId)
                    return Result<bool>.Failure([FolderErrors.ParentNotFound]);
            }
            if (input.Kind == "page")
            {
                var items = await pages.GetByIdsForUserAsync(input.ItemIds, input.UserId, token);
                if (items.Count != input.ItemIds.Count || items.Any(page => page.SpaceId != input.SpaceId))
                    return Result<bool>.Failure([PageErrors.NotFound]);
                if (input.ParentId is { } parentId)
                {
                    var parent = await pages.FindByIdForUserAsync(parentId, input.UserId, token);
                    if (parent is null || parent.SpaceId != input.SpaceId)
                        return Result<bool>.Failure([PageErrors.ParentNotFound]);
                    var parents = await pages.GetParentMapForUserAsync(input.UserId, token);
                    if (items.Any(page => PageHierarchy.WouldCreateCycle(page.Id, parentId, parents)))
                        return Result<bool>.Failure([PageErrors.CycleDetected]);
                }
                var byId = items.ToDictionary(page => page.Id);
                for (var index = 0; index < input.ItemIds.Count; index++)
                {
                    var page = byId[input.ItemIds[index]];
                    page.Move(input.ParentId, index, input.FolderId);
                    await pages.UpdateAsync(page, token);
                }
            }
            else
            {
                var all = await folders.GetForUserAsync(input.UserId, input.SpaceId, token);
                var byId = all.ToDictionary(folder => folder.Id);
                if (input.ItemIds.Any(id => !byId.ContainsKey(id)))
                    return Result<bool>.Failure([FolderErrors.NotFound]);
                if (input.ParentId is { } parentId && input.ItemIds.Any(id => Folder.Subtree(id, all).Any(folder => folder.Id == parentId)))
                    return Result<bool>.Failure([FolderErrors.CycleDetected]);
                for (var index = 0; index < input.ItemIds.Count; index++)
                {
                    var folder = byId[input.ItemIds[index]];
                    folder.Move(input.ParentId, index);
                    await folders.UpdateAsync(folder, token);
                }
            }
            // Validate the whole drop before changing tracked entities, then save once while locked.
            await ctx.CommitAsync(token);
            return Result<bool>.Success(true);
        }, new UnitOfWorkOptions { CommitOnSuccess = false }, ct);
        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value);
    }
}
