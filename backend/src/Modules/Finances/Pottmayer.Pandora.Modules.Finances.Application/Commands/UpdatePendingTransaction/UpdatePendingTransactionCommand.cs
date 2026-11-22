using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Abstractions;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdatePendingTransaction;

public sealed record UpdatePendingTransactionInput(
    Guid UserId,
    Guid Id,
    string Kind,
    decimal? Amount,
    DateOnly OccurredOn,
    string Description,
    string? Payee,
    string? Notes,
    Guid? SystemCategoryId,
    Guid? UserCategoryId,
    Guid? SuggestedStatementId);

/// <summary>
/// Edits a pending suggestion's details before deciding it (e.g. correcting the kind or category an
/// importer guessed wrong). Fails once the suggestion has been approved, rejected, or linked.
/// </summary>
[RequiresPermission(FinancesPermissions.Pending.Update)]
public sealed class UpdatePendingTransactionCommand(UpdatePendingTransactionInput input)
    : CommandBase<UpdatePendingTransactionInput, PendingTransactionDto>(input);
