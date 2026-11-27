using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UndoPositionMovement;

public sealed record UndoPositionMovementInput(Guid UserId, Guid PositionId);

/// <summary>
/// Undoes a position's latest movement and voids its ledger entry. Undoing the only movement deletes
/// the position. Returns whether the position still exists.
/// </summary>
[RequiresPermission(FinancesPermissions.Investments.Delete)]
public sealed class UndoPositionMovementCommand(UndoPositionMovementInput input) : CommandBase<UndoPositionMovementInput, bool>(input);
