namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record CreateTransferRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal AmountOut,
    decimal? AmountIn,
    decimal? FxRate,
    DateOnly OccurredOn,
    string Description,
    string? Notes);
