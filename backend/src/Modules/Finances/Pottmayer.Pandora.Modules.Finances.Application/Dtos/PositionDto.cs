using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

/// <summary>
/// A position with its asset and current value. <see cref="CurrentValue"/> is the latest reported
/// value, or the cost when none was reported (<see cref="ValuedOn"/> null); a closed position is worth 0.
/// </summary>
public sealed record PositionDto(
    Guid Id,
    Guid AccountId,
    Guid AssetId,
    string AssetName,
    string? AssetCode,
    Guid AssetClassId,
    string Behavior,
    string Status,
    string Currency,
    decimal? Quantity,
    decimal? AverageCost,
    decimal? InvestedAmount,
    DateOnly? AppliedOn,
    string? Index,
    decimal? IndexPercent,
    decimal? SpreadRate,
    DateOnly? MaturityOn,
    decimal CostBasis,
    decimal CurrentValue,
    DateOnly? ValuedOn,
    DateTimeOffset? ClosedAt)
{
    public static PositionDto From(Position p, Asset asset, PositionValuation? latest) =>
        new(p.Id, p.AccountId, p.AssetId, asset.Name, asset.Code, asset.AssetClassId, p.Behavior.Value, p.Status.Value,
            asset.Currency.Value, p.Quantity, p.AverageCost, p.InvestedAmount, p.AppliedOn, p.Index?.Value, p.IndexPercent,
            p.SpreadRate, p.MaturityOn, p.CostBasis,
            !p.IsOpen ? 0m : latest?.Value ?? p.CostBasis,
            latest?.ValuedOn, p.ClosedAt);
}

public sealed record PositionMovementDto(
    Guid Id, string Kind, DateOnly OccurredOn, decimal? Quantity, decimal? UnitPrice, decimal? GrossAmount, decimal Costs,
    decimal TaxWithheld, decimal? NetAmount, string? IncomeKind, decimal? SplitFactor, decimal? RealizedResult,
    Guid? TransactionId, string? Notes, DateTimeOffset CreatedAt)
{
    public static PositionMovementDto From(PositionMovement m) =>
        new(m.Id, m.Kind.Value, m.OccurredOn, m.Quantity, m.UnitPrice, m.GrossAmount, m.Costs, m.TaxWithheld, m.NetAmount,
            m.IncomeKind?.Value, m.SplitFactor, m.RealizedResult, m.TransactionId, m.Notes, m.CreatedAt);
}

public sealed record PositionValuationDto(Guid Id, Guid PositionId, DateOnly ValuedOn, decimal Value, string Source)
{
    public static PositionValuationDto From(PositionValuation v) => new(v.Id, v.PositionId, v.ValuedOn, v.Value, v.Source.Value);
}

/// <summary>A position with its movements (oldest first) and reported values (newest first).</summary>
public sealed record PositionDetailDto(
    PositionDto Position, IReadOnlyList<PositionMovementDto> Movements, IReadOnlyList<PositionValuationDto> Valuations);

/// <summary>Totals per currency; nothing is converted between currencies.</summary>
public sealed record InvestmentsSummaryDto(
    IReadOnlyList<InvestmentTotalDto> Totals,
    IReadOnlyList<InvestmentGroupDto> ByClass,
    IReadOnlyList<InvestmentGroupDto> ByAccount,
    IReadOnlyList<PositionDto> UpcomingMaturities);

public sealed record InvestmentTotalDto(string Currency, decimal Value, decimal Cost);

/// <summary>A slice of the portfolio: <see cref="Id"/> is the asset class or the account.</summary>
public sealed record InvestmentGroupDto(Guid Id, string Currency, decimal Value);
