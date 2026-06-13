namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

/// <summary>
/// Balance of an account derived from its ledger: <see cref="Posted"/> is the signed sum of posted
/// entries; <see cref="Projected"/> also includes pending (scheduled/future) ones.
/// </summary>
public sealed record AccountBalanceDto(
    Guid AccountId,
    string Currency,
    decimal Posted,
    decimal Projected);
