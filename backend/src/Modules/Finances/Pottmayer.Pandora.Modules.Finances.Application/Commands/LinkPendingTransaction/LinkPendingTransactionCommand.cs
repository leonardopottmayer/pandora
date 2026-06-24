using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.LinkPendingTransaction;

public sealed record LinkPendingTransactionInput(Guid UserId, Guid PendingId, Guid TransactionId);

/// <summary>
/// Resolves an import suggestion against a transaction the user already entered manually, instead
/// of creating a duplicate. See <see cref="LinkPendingTransactionCommandHandler"/> for the full effect.
/// </summary>
public sealed class LinkPendingTransactionCommand(LinkPendingTransactionInput input)
    : CommandBase<LinkPendingTransactionInput, PendingTransactionDto>(input);
