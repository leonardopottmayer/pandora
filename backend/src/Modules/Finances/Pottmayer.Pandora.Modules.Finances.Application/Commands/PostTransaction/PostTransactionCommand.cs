using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.PostTransaction;

public sealed record PostTransactionInput(Guid UserId, Guid TransactionId);

public sealed class PostTransactionCommand(PostTransactionInput input)
    : CommandBase<PostTransactionInput, TransactionDto>(input);
