using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateCardNumber;

public sealed record CreateCardNumberInput(Guid UserId, Guid CardId, string Kind, string LastFour, string? Nickname);

/// <summary>Adds a physical or virtual number to a card. Managing numbers is editing the card.</summary>
[RequiresPermission(FinancesPermissions.Cards.Update)]
public sealed class CreateCardNumberCommand(CreateCardNumberInput input)
    : CommandBase<CreateCardNumberInput, CardNumberDto>(input);
