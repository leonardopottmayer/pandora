using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.SetCardArchived;

public sealed record SetCardArchivedInput(Guid UserId, Guid CardId, bool Archived);

/// <summary>Archives or unarchives a card. Idempotent: setting the current state is a no-op.</summary>
[RequiresPermission(FinancesPermissions.Cards.Update)]
public sealed class SetCardArchivedCommand(SetCardArchivedInput input) : CommandBase<SetCardArchivedInput, CardDto>(input);
