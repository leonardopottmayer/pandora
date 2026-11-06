using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteCard;

public sealed record DeleteCardInput(Guid UserId, Guid CardId);

/// <summary>Permanently removes a card. Fails if it already has any statement history.</summary>
[RequiresPermission(FinancesPermissions.Cards.Delete)]
public sealed class DeleteCardCommand(DeleteCardInput input) : CommandBase<DeleteCardInput, bool>(input);
