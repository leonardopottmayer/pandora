using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.SetCardArchived;

public sealed record SetCardArchivedInput(Guid UserId, Guid CardId, bool Archived);

/// <summary>Archives or unarchives a card. Idempotent: setting the current state is a no-op.</summary>
public sealed class SetCardArchivedCommand(SetCardArchivedInput input) : CommandBase<SetCardArchivedInput, CardDto>(input);
