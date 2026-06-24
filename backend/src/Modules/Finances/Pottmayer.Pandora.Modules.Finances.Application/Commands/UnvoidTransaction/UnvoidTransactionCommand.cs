using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UnvoidTransaction;

public sealed record UnvoidTransactionInput(Guid UserId, Guid TransactionId, bool UnvoidEntirePlan = false);

/// <summary>
/// Undoes a void, reapplying the transaction's effect on the destination's totals. Mirrors
/// <see cref="VoidTransaction.VoidTransactionCommand"/>'s transfer-pair and installment-plan handling.
/// </summary>
public sealed class UnvoidTransactionCommand(UnvoidTransactionInput input)
    : CommandBase<UnvoidTransactionInput, TransactionDto>(input);
