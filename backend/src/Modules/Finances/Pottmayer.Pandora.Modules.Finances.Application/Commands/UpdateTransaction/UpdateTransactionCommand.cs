using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UpdateTransaction;

/// <summary>Cosmetic edit only: value, destination and kind are immutable once recorded.</summary>
public sealed record UpdateTransactionInput(
    Guid UserId,
    Guid TransactionId,
    string Description,
    string? Payee,
    string? Notes,
    Guid? SystemCategoryId,
    Guid? UserCategoryId);

/// <summary>Edits a transaction's cosmetic fields. Fails on a voided transaction.</summary>
public sealed class UpdateTransactionCommand(UpdateTransactionInput input)
    : CommandBase<UpdateTransactionInput, TransactionDto>(input);
