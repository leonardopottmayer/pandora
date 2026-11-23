using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.DeleteCardNumber;

public sealed record DeleteCardNumberInput(Guid UserId, Guid CardId, Guid NumberId);

/// <summary>
/// Removes a card number. One that a transaction used is archived instead, so the transaction keeps
/// saying which number it was made with. Returns <c>true</c> when it was deleted, <c>false</c> when archived.
/// </summary>
[RequiresPermission(FinancesPermissions.Cards.Update)]
public sealed class DeleteCardNumberCommand(DeleteCardNumberInput input)
    : CommandBase<DeleteCardNumberInput, bool>(input);
