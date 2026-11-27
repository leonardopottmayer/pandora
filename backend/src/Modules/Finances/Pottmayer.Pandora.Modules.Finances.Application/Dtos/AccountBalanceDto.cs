namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

/// <summary>
/// Balance of an account derived from its ledger: <see cref="Posted"/> is the signed sum of posted
/// entries; <see cref="Projected"/> also includes pending (scheduled/future) ones.
/// <see cref="PositionsValue"/> is the current value of the account's open investment positions, and
/// <see cref="Total"/> = <see cref="Posted"/> + <see cref="PositionsValue"/>.
/// </summary>
public sealed record AccountBalanceDto(
    Guid AccountId,
    string Currency,
    decimal Posted,
    decimal Projected,
    decimal PositionsValue = 0m,
    decimal Total = 0m);
