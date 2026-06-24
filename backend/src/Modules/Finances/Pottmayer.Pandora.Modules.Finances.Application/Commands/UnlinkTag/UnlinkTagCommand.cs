using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UnlinkTag;

public sealed record UnlinkTagInput(Guid UserId, Guid TagId, string EntityType, Guid EntityId);

/// <summary>Detaches a tag from an entity. Fails if that exact link doesn't exist.</summary>
public sealed class UnlinkTagCommand(UnlinkTagInput input)
    : CommandBase<UnlinkTagInput, bool>(input);
