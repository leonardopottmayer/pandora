using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Notes.Application.Commands.DeleteSpace;

public sealed record DeleteSpaceInput(Guid UserId, Guid SpaceId);

/// <summary>Soft-deletes an empty space. One with pages, or the default one, is refused.</summary>
public sealed class DeleteSpaceCommand(DeleteSpaceInput input)
    : CommandBase<DeleteSpaceInput, bool>(input);
