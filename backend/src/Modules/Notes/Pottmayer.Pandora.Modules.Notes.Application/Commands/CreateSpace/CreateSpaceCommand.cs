using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.CreateSpace;

public sealed record CreateSpaceInput(Guid UserId, string Name, string? Icon, string? Color, string? Description);

/// <summary>Creates a space. The user's first one becomes their default.</summary>
public sealed class CreateSpaceCommand(CreateSpaceInput input)
    : CommandBase<CreateSpaceInput, SpaceDto>(input);
