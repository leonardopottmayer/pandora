using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.SetDefaultSpace;

public sealed record SetDefaultSpaceInput(Guid UserId, Guid SpaceId);

/// <summary>Makes a space the user's default, taking the mark from the previous one. An archived space comes back.</summary>
[RequiresPermission(NotesPermissions.Spaces.Update)]
public sealed class SetDefaultSpaceCommand(SetDefaultSpaceInput input)
    : CommandBase<SetDefaultSpaceInput, SpaceDto>(input);
