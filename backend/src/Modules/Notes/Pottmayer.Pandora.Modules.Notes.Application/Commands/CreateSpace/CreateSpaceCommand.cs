using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.CreateSpace;

public sealed record CreateSpaceInput(Guid UserId, string Name, string? Icon, string? Color, string? Description);

/// <summary>Creates a space. The user's first one becomes their default.</summary>
[RequiresPermission(NotesPermissions.Spaces.Create)]
public sealed class CreateSpaceCommand(CreateSpaceInput input)
    : CommandBase<CreateSpaceInput, SpaceDto>(input);
