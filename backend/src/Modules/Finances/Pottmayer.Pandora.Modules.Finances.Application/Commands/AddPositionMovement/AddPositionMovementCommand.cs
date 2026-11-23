using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.AddPositionMovement;

public sealed record AddPositionMovementInput(
    Guid UserId,
    Guid PositionId,
    string Kind,
    DateOnly OccurredOn,
    decimal? Quantity = null,
    decimal? UnitPrice = null,
    decimal? GrossAmount = null,
    decimal Costs = 0m,
    decimal TaxWithheld = 0m,
    string? IncomeKind = null,
    decimal? SplitFactor = null,
    string? Notes = null);

/// <summary>Records a movement on a position and, when it moves money, its ledger entry (INV-7).</summary>
[RequiresPermission(FinancesPermissions.Investments.Update)]
public sealed class AddPositionMovementCommand(AddPositionMovementInput input) : CommandBase<AddPositionMovementInput, PositionDto>(input);
