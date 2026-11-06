using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

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
[RequiresPermission(FinancesPermissions.Cards.Create)]
public sealed class CreateCardCommand(CreateCardInput input) : CommandBase<CreateCardInput, CardDto>(input);
