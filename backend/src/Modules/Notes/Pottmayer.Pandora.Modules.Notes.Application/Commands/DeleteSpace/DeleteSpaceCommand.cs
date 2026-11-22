using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.DeleteSpace;

public sealed record DeleteSpaceInput(Guid UserId, Guid SpaceId);

/// <summary>Soft-deletes an empty space. One with pages, or the default one, is refused.</summary>
[RequiresPermission(NotesPermissions.Spaces.Delete)]
public sealed class DeleteSpaceCommand(DeleteSpaceInput input)
    : CommandBase<DeleteSpaceInput, bool>(input);
