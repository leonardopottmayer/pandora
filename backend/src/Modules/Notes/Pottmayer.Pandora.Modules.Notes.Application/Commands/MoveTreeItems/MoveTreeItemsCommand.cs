using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.MoveTreeItems;

public sealed record MoveTreeItemsInput(Guid UserId, Guid SpaceId, string Kind, Guid? ParentId, Guid? FolderId, IReadOnlyList<Guid> ItemIds);

/// <summary>Moves and orders one kind of tree item in one space, committing the whole drop atomically.</summary>
[RequiresPermission(NotesPermissions.Spaces.Read)]
public sealed class MoveTreeItemsCommand(MoveTreeItemsInput input) : CommandBase<MoveTreeItemsInput, bool>(input);
