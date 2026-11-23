using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateCardNumber;

public sealed record UpdateCardNumberInput(Guid UserId, Guid CardId, Guid NumberId, string Kind, string LastFour, string? Nickname);

/// <summary>Edits one of a card's numbers. Fails on an archived number.</summary>
[RequiresPermission(FinancesPermissions.Cards.Update)]
public sealed class UpdateCardNumberCommand(UpdateCardNumberInput input)
    : CommandBase<UpdateCardNumberInput, CardNumberDto>(input);
