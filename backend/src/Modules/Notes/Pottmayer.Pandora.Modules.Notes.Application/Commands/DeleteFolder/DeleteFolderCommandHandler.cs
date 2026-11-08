using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Services;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.DeleteFolder;

public sealed class DeleteFolderCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<DeleteFolderCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeleteFolderCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var folders = ctx.AcquireRepository<IFolderRepository>();
            var folder = await folders.FindByIdForUserAsync(input.FolderId, input.UserId, token);
            if (folder is null)
                return Result<bool>.Failure([FolderErrors.NotFound]);

            var subtree = Folder.Subtree(folder.Id, await folders.GetForUserAsync(input.UserId, folder.SpaceId, token));
            var ids = subtree.Select(f => f.Id).ToHashSet();

            var pages = await ctx.AcquireRepository<IPageRepository>().GetTreeForUserAsync(input.UserId, includeArchived: true, token);
            await PageDeletion.DeleteSubtreesAsync(
                ctx, [.. pages.Where(p => p.FolderId is { } f && ids.Contains(f)).Select(p => p.Id)], input.UserId, timeProvider, token);

            foreach (var item in subtree)
            {
                item.Delete(timeProvider);
                await folders.UpdateAsync(item, token);
            }

            return Result<bool>.Success(true);
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(true);
    }
}
