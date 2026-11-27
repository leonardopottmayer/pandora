namespace Pottmayer.Pandora.Modules.Finances.Presentation.Requests;

public sealed record CreateAssetClassRequest(string Name, string Behavior);

public sealed record RenameAssetClassRequest(string Name);

public sealed record CreateAssetRequest(Guid AssetClassId, string Name, string? Code, string? Isin, string? Issuer, string Currency);

public sealed record UpdateAssetRequest(string Name, string? Code, string? Isin, string? Issuer);

/// <summary>A new position and its first movement (<c>opening</c> or <c>buy</c>).</summary>
public sealed record CreatePositionRequest(
    Guid AccountId,
    Guid AssetId,
    string Kind,
    DateOnly OccurredOn,
    decimal? Quantity = null,
    decimal? UnitPrice = null,
    decimal? AverageCost = null,
    decimal? InvestedAmount = null,
    decimal? CurrentValue = null,
    decimal Costs = 0m,
    string? Index = null,
    decimal? IndexPercent = null,
    decimal? SpreadRate = null,
    DateOnly? MaturityOn = null,
    string? Notes = null);

public sealed record AddPositionMovementRequest(
    string Kind,
    DateOnly OccurredOn,
    decimal? Quantity = null,
    decimal? UnitPrice = null,
    decimal? GrossAmount = null,
    decimal Costs = 0m,
    decimal TaxWithheld = 0m,
    string? IncomeKind = null,
    decimal? SplitFactor = null,
    string? Notes = null);

public sealed record ReportPositionValueRequest(DateOnly ValuedOn, decimal Value);

public sealed record ReportPositionValuesRequest(DateOnly ValuedOn, IReadOnlyList<PositionValueRequestItem> Items);

public sealed record PositionValueRequestItem(Guid PositionId, decimal Value);
