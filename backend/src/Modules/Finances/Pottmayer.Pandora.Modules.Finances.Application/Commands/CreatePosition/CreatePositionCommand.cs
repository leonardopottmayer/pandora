using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreatePosition;

/// <param name="Kind">The first movement: <c>opening</c> (no cash, INV-6) or <c>buy</c> (cash leaves now).</param>
/// <param name="CurrentValue">Optional value at the date; stored as the first reported value.</param>
public sealed record CreatePositionInput(
    Guid UserId,
    Guid AccountId,
    Guid AssetId,
    string Kind,
    DateOnly OccurredOn,
    decimal? Quantity = null,
    decimal? UnitPrice = null,
    decimal? AverageCost = null,
    decimal? InvestedAmount = null,
    decimal? CurrentValue = null,
    decimal Costs = 0m,
    string? Index = null,
    decimal? IndexPercent = null,
    decimal? SpreadRate = null,
    DateOnly? MaturityOn = null,
    string? Notes = null);

/// <summary>Opens a position in an account with its first movement.</summary>
[RequiresPermission(FinancesPermissions.Investments.Create)]
public sealed class CreatePositionCommand(CreatePositionInput input) : CommandBase<CreatePositionInput, PositionDto>(input);
