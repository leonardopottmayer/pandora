using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

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
    Guid? DefaultPaymentAccountId);

/// <summary>Edits a card's mutable fields. Currency is intentionally absent: it is fixed at creation.</summary>
public sealed class UpdateCardCommand(UpdateCardInput input) : CommandBase<UpdateCardInput, CardDto>(input);
