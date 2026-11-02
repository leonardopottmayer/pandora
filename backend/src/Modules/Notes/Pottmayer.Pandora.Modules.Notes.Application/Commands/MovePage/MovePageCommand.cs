using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.MovePage;

public sealed record MovePageInput(
    Guid UserId,
    Guid PageId,
    Guid? ParentId,
    int OrderIndex,
    Guid? SpaceId = null);

/// <summary>
/// Reparents and repositions a page in the sidebar tree (the drag-and-drop path). Rejects a move that
/// would create a cycle — under itself or one of its own descendants. The page lands in its new
/// parent's space, or at the top of <see cref="MovePageInput.SpaceId"/>; changing space takes the
/// whole subtree along.
/// </summary>
public sealed class MovePageCommand(MovePageInput input)
    : CommandBase<MovePageInput, PageDto>(input);
