using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.SetDefaultSpace;

public sealed record SetDefaultSpaceInput(Guid UserId, Guid SpaceId);

/// <summary>Makes a space the user's default, taking the mark from the previous one. An archived space comes back.</summary>
public sealed class SetDefaultSpaceCommand(SetDefaultSpaceInput input)
    : CommandBase<SetDefaultSpaceInput, SpaceDto>(input);
