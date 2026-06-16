using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.UnvoidTransaction;

public sealed record UnvoidTransactionInput(Guid UserId, Guid TransactionId, bool UnvoidEntirePlan = false);

public sealed class UnvoidTransactionCommand(UnvoidTransactionInput input)
    : CommandBase<UnvoidTransactionInput, TransactionDto>(input);
