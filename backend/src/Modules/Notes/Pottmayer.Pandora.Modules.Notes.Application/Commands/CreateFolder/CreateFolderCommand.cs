using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.CreateFolder;

public sealed record CreateFolderInput(
    Guid UserId, string Name, Guid? ParentId, Guid? SpaceId, string? Icon, string? Description);

/// <summary>
/// Creates a folder at the end of its parent, or at the top of a space (the named one, or the
/// default). A folder inside another lives in that folder's space.
/// </summary>
[RequiresPermission(NotesPermissions.Folders.Create)]
public sealed class CreateFolderCommand(CreateFolderInput input)
    : CommandBase<CreateFolderInput, FolderDto>(input);
