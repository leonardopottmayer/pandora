using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.ReverseTransaction;

public sealed record ReverseTransactionInput(Guid UserId, Guid TransactionId, string? Description);

public sealed class ReverseTransactionCommand(ReverseTransactionInput input)
    : CommandBase<ReverseTransactionInput, TransactionDto>(input);
