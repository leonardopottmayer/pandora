using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.RejectPendingTransaction;

public sealed record RejectPendingTransactionInput(Guid UserId, Guid Id, string? Reason);

public sealed class RejectPendingTransactionCommand(RejectPendingTransactionInput input)
    : CommandBase<RejectPendingTransactionInput, PendingTransactionDto>(input);
