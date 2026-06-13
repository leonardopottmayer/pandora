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

public sealed class CreateTransactionCommand(CreateTransactionInput input)
    : CommandBase<CreateTransactionInput, TransactionDto>(input);
