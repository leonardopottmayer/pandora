using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateCard;

public sealed record CreateCardInput(
    Guid UserId,
    string Name,
    string? Brand,
    string? LastFour,
    decimal? CreditLimit,
    int ClosingDay,
    int DueDay,
    string Currency,
    Guid AccountId);

/// <summary>Registers a new credit card for the user, owned by one of the user's accounts.</summary>
public sealed class CreateCardCommand(CreateCardInput input) : CommandBase<CreateCardInput, CardDto>(input);
