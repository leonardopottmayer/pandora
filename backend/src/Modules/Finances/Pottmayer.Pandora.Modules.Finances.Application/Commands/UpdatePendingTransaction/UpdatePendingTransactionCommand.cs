using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

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
public sealed class UpdatePendingTransactionCommand(UpdatePendingTransactionInput input)
    : CommandBase<UpdatePendingTransactionInput, PendingTransactionDto>(input);
