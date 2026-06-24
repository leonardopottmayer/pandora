using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateTransaction;

public sealed record CreateTransactionInput(
    Guid UserId,
    Guid? AccountId,
    Guid? CardId,
    Guid? CardStatementId,
    string Kind,
    decimal Amount,
    DateOnly OccurredOn,
    string Description,
    string? Payee,
    string? Notes,
    Guid? SystemCategoryId,
    Guid? UserCategoryId,
    int Installments = 1);

/// <summary>
/// Creates a single movement against an account or a card statement, or — when
/// <see cref="CreateTransactionInput.Installments"/> is greater than 1 — a full installment
/// purchase split across that many consecutive card statements. Transfers are created through
/// <c>CreateTransfer</c> instead, never through this command.
/// </summary>
public sealed class CreateTransactionCommand(CreateTransactionInput input)
    : CommandBase<CreateTransactionInput, TransactionDto>(input);
