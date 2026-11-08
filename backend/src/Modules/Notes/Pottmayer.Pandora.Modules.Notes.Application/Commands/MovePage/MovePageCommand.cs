using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.MovePage;

public sealed record MovePageInput(
    Guid UserId,
    Guid PageId,
    Guid? ParentId,
    int OrderIndex,
    Guid? SpaceId = null,
    Guid? FolderId = null);

/// <summary>
/// Reparents and repositions a page in the sidebar tree (the drag-and-drop path). Rejects a move that
/// would create a cycle — under itself or one of its own descendants. The page lands in its new
/// parent's space, or at the top of <see cref="MovePageInput.SpaceId"/>; changing space takes the
/// whole subtree along. With no parent, <see cref="MovePageInput.FolderId"/> puts it in a folder, in that
/// folder's space.
/// </summary>
[RequiresPermission(NotesPermissions.Pages.Update)]
public sealed class MovePageCommand(MovePageInput input)
    : CommandBase<MovePageInput, PageDto>(input);
