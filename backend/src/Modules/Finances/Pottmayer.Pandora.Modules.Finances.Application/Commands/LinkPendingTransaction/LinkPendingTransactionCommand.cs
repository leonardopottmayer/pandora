using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.LinkPendingTransaction;

public sealed record LinkPendingTransactionInput(Guid UserId, Guid PendingId, Guid TransactionId);

public sealed class LinkPendingTransactionCommand(LinkPendingTransactionInput input)
    : CommandBase<LinkPendingTransactionInput, PendingTransactionDto>(input);
