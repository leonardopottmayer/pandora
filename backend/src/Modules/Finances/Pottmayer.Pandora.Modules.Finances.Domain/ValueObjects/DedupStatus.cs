using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>
/// How an incoming row compares to what's already on file: <see cref="New"/> (no match),
/// <see cref="Suspected"/> or <see cref="Certain"/> (likely/near-certain duplicate, pending user
/// confirmation) and <see cref="Matched"/> (confirmed, linked to an existing record). Shared between
/// <see cref="Aggregates.ImportRow"/> and <see cref="Aggregates.PendingTransaction"/>, which both
/// surface the same dedup decision from the import pipeline.
/// </summary>
public sealed class DedupStatus : IDomainValue<DedupStatus>
{
    public static readonly DedupStatus New = new("new");
    public static readonly DedupStatus Certain = new("certain");
    public static readonly DedupStatus Suspected = new("suspected");
    public static readonly DedupStatus Matched = new("matched");

    private static readonly Dictionary<string, DedupStatus> All = new()
    {
        [New.Value] = New,
        [Certain.Value] = Certain,
        [Suspected.Value] = Suspected,
        [Matched.Value] = Matched
    };

    public string Value { get; }

    private DedupStatus(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static DedupStatus FromValue(string value) =>
        All.TryGetValue(value, out var status)
            ? status
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown dedup status.");

    public override string ToString() => Value;
}
