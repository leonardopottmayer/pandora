using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.MoveFolder;

public sealed record MoveFolderInput(Guid UserId, Guid FolderId, Guid? ParentId, int OrderIndex);

/// <summary>
/// Puts a folder in another folder of the same space, or at the top of its space, at a position
/// (the drag-and-drop path). Refuses a move into itself or one of its subfolders.
/// </summary>
[RequiresPermission(NotesPermissions.Folders.Update)]
public sealed class MoveFolderCommand(MoveFolderInput input)
    : CommandBase<MoveFolderInput, FolderDto>(input);
