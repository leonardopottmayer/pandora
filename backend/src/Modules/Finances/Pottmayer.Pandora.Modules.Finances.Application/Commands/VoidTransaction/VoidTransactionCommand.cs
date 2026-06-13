using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.VoidTransaction;

public sealed record VoidTransactionInput(Guid UserId, Guid TransactionId, string? Reason, bool VoidEntirePlan = false);

public sealed class VoidTransactionCommand(VoidTransactionInput input)
    : CommandBase<VoidTransactionInput, TransactionDto>(input);
