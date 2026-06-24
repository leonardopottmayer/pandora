using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.PostTransaction;

public sealed record PostTransactionInput(Guid UserId, Guid TransactionId);

/// <summary>Effects a scheduled (pending) transaction immediately. No-op unless it is still pending.</summary>
public sealed class PostTransactionCommand(PostTransactionInput input)
    : CommandBase<PostTransactionInput, TransactionDto>(input);
