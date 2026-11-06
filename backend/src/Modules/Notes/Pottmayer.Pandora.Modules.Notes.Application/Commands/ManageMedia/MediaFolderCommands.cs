using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.ManageMedia;

/// <summary>What can happen to a folder of the media library.</summary>
public abstract record FolderChange
{
    public sealed record Create(string Name, Guid? ParentId) : FolderChange;

    public sealed record Rename(string Name) : FolderChange;

    /// <summary>Under another folder, or to the top (null). Refused when it would go under itself.</summary>
    public sealed record Move(Guid? ParentId) : FolderChange;

    /// <summary>Moves what the folder holds — subfolders and items — up a level, then removes it.</summary>
    public sealed record Delete : FolderChange;
}

/// <summary><paramref name="FolderId"/> is ignored for <see cref="FolderChange.Create"/>.</summary>
public sealed record ChangeMediaFolderInput(Guid UserId, Guid? FolderId, FolderChange Change);

/// <summary>Creates, renames, moves or deletes a folder; answers the folder as it ends up, or as it was when deleted.</summary>
[RequiresPermission(NotesPermissions.Media.Update)]
public sealed class ChangeMediaFolderCommand(ChangeMediaFolderInput input)
    : CommandBase<ChangeMediaFolderInput, MediaFolderDto>(input);

public sealed class ChangeMediaFolderCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<ChangeMediaFolderCommand, MediaFolderDto>
{
    private const int MaxNameLength = 100;

    protected override async Task<Result<MediaFolderDto>> HandleAsync(ChangeMediaFolderCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var name = input.Change switch
        {
            FolderChange.Create create => create.Name,
            FolderChange.Rename rename => rename.Name,
            _ => null,
        };
        if (input.Change is FolderChange.Create or FolderChange.Rename &&
            (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength))
            return Fail(MediaErrors.InvalidName);

        var result = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var folders = ctx.AcquireRepository<IMediaFolderRepository>();
            var items = ctx.AcquireRepository<IMediaItemRepository>();

            // A parent named on create or move must be one of the user's folders.
            var parentId = input.Change switch
            {
                FolderChange.Create create => create.ParentId,
                FolderChange.Move move => move.ParentId,
                _ => null,
            };
            if (parentId is { } parent && await folders.FindByIdForUserAsync(parent, input.UserId, token) is null)
                return Result<MediaFolderDto>.Failure([MediaErrors.FolderNotFound]);

            if (input.Change is FolderChange.Create)
            {
                var created = MediaFolder.Create(input.UserId, parentId, name!, timeProvider);
                await folders.AddAsync(created, token);
                return Result<MediaFolderDto>.Success(MediaFolderDto.From(created, 0));
            }

            var folder = input.FolderId is { } id ? await folders.FindByIdForUserAsync(id, input.UserId, token) : null;
            if (folder is null)
                return Result<MediaFolderDto>.Failure([MediaErrors.FolderNotFound]);

            switch (input.Change)
            {
                case FolderChange.Rename:
                    folder.Rename(name!);
                    break;

                case FolderChange.Move move:
                    // The same upward walk the page tree uses: a cycle is reaching the folder being moved.
                    var parents = (await folders.GetForUserAsync(input.UserId, token)).ToDictionary(f => f.Id, f => f.ParentId);
                    if (PageHierarchy.WouldCreateCycle(folder.Id, move.ParentId, parents))
                        return Result<MediaFolderDto>.Failure([MediaErrors.FolderCycle]);
                    folder.MoveTo(move.ParentId);
                    break;

                case FolderChange.Delete:
                    foreach (var child in (await folders.GetForUserAsync(input.UserId, token)).Where(f => f.ParentId == folder.Id))
                    {
                        child.MoveTo(folder.ParentId);
                        await folders.UpdateAsync(child, token);
                    }
                    foreach (var item in await items.GetInFolderAsync(input.UserId, folder.Id, token))
                    {
                        item.MoveTo(folder.ParentId);
                        await items.UpdateAsync(item, token);
                    }
                    await folders.RemoveAsync(folder, token);
                    return Result<MediaFolderDto>.Success(MediaFolderDto.From(folder, 0));
            }

            await folders.UpdateAsync(folder, token);
            var counts = await items.CountAsync(input.UserId, token);
            return Result<MediaFolderDto>.Success(MediaFolderDto.From(folder, counts.ByFolder.GetValueOrDefault(folder.Id)));
        }, cancellationToken: ct);

        return result.IsFailure ? Fail([.. result.Errors]) : Ok(result.Value!);
    }
}
