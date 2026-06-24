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

/// <summary>
/// Moves money between two of the user's accounts: creates both transfer legs (outflow on the
/// source, inflow on the destination) atomically. Cross-currency transfers require both amounts
/// and an FX rate; same-currency transfers mirror the single amount.
/// </summary>
public sealed class CreateTransferCommand(CreateTransferInput input)
    : CommandBase<CreateTransferInput, IReadOnlyList<TransactionDto>>(input);
