using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.UpdateSpace;

public sealed record UpdateSpaceInput(
    Guid UserId, Guid SpaceId, string Name, string? Icon, string? Color, string? Description);

/// <summary>Replaces a space's name, icon, color and description.</summary>
[RequiresPermission(NotesPermissions.Spaces.Update)]
public sealed class UpdateSpaceCommand(UpdateSpaceInput input)
    : CommandBase<UpdateSpaceInput, SpaceDto>(input);
