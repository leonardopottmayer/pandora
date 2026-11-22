using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.SetSpaceArchived;

public sealed record SetSpaceArchivedInput(Guid UserId, Guid SpaceId, bool Archived);

/// <summary>
/// Hides a space from the list, or brings it back. Its pages stay as they are: still found by search,
/// still reached by links. The default space cannot be archived.
/// </summary>
[RequiresPermission(NotesPermissions.Spaces.Update)]
public sealed class SetSpaceArchivedCommand(SetSpaceArchivedInput input)
    : CommandBase<SetSpaceArchivedInput, SpaceDto>(input);
