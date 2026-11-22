using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.DeleteFolder;

public sealed record DeleteFolderInput(Guid UserId, Guid FolderId);

/// <summary>Soft-deletes a folder with everything in it: its subfolders, their pages and those pages' sub-pages.</summary>
[RequiresPermission(NotesPermissions.Folders.Delete)]
public sealed class DeleteFolderCommand(DeleteFolderInput input)
    : CommandBase<DeleteFolderInput, bool>(input);
