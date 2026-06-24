using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.VoidTransaction;

public sealed record VoidTransactionInput(Guid UserId, Guid TransactionId, string? Reason, bool VoidEntirePlan = false);

/// <summary>
/// Cancels a posted transaction, undoing its effect on the destination's totals. Transfers void as
/// a pair; an installment can be voided alone or, via <see cref="VoidTransactionInput.VoidEntirePlan"/>,
/// together with every other still-open installment of its plan.
/// </summary>
public sealed class VoidTransactionCommand(VoidTransactionInput input)
    : CommandBase<VoidTransactionInput, TransactionDto>(input);
