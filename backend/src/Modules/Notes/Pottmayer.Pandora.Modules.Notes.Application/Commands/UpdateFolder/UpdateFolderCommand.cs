using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.UpdateFolder;

public sealed record UpdateFolderInput(Guid UserId, Guid FolderId, string Name, string? Icon, string? Description);

/// <summary>Replaces a folder's name, icon and description.</summary>
[RequiresPermission(NotesPermissions.Folders.Update)]
public sealed class UpdateFolderCommand(UpdateFolderInput input)
    : CommandBase<UpdateFolderInput, FolderDto>(input);
