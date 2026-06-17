using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.ApprovePendingTransaction;

public sealed record ApprovePendingTransactionInput(Guid UserId, Guid Id);

public sealed class ApprovePendingTransactionCommand(ApprovePendingTransactionInput input)
    : CommandBase<ApprovePendingTransactionInput, TransactionDto>(input);
