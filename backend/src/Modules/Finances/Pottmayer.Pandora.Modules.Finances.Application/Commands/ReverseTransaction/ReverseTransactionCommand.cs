using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.ReverseTransaction;

public sealed record ReverseTransactionInput(Guid UserId, Guid TransactionId, string? Description);

/// <summary>
/// Creates an opposite-effect counter-entry for a posted transaction, dated today. The kind of
/// mirror depends on what the original targeted (account, transfer, statement purchase, or
/// statement payment) — see <see cref="ReverseTransactionCommandHandler"/>.
/// </summary>
public sealed class ReverseTransactionCommand(ReverseTransactionInput input)
    : CommandBase<ReverseTransactionInput, TransactionDto>(input);
