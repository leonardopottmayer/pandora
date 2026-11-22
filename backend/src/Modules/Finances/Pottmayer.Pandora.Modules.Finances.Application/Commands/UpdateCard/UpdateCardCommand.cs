using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateCard;

public sealed record UpdateCardInput(
    Guid UserId,
    Guid CardId,
    string Name,
    string? Brand,
    string? LastFour,
    decimal? CreditLimit,
    int ClosingDay,
    int DueDay,
    Guid AccountId);

/// <summary>Edits a card's mutable fields. Currency is intentionally absent: it is fixed at creation.</summary>
[RequiresPermission(FinancesPermissions.Cards.Update)]
public sealed class UpdateCardCommand(UpdateCardInput input) : CommandBase<UpdateCardInput, CardDto>(input);
