using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Finances.Application.Commands.CreateTransfer;

public sealed record CreateTransferInput(
    Guid UserId,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal AmountOut,
    decimal? AmountIn,
    decimal? FxRate,
    DateOnly OccurredOn,
    string Description,
    string? Notes);

public sealed class CreateTransferCommand(CreateTransferInput input)
    : CommandBase<CreateTransferInput, IReadOnlyList<TransactionDto>>(input);
