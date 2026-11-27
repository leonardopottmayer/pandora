using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>Where a position's reported value came from.</summary>
public sealed class ValuationSource : IDomainValue<ValuationSource>
{
    public static readonly ValuationSource Manual = new("manual");
    public static readonly ValuationSource Import = new("import");
    public static readonly ValuationSource Quote = new("quote");

    private static readonly Dictionary<string, ValuationSource> All = new()
    {
        [Manual.Value] = Manual,
        [Import.Value] = Import,
        [Quote.Value] = Quote
    };

    public string Value { get; }

    private ValuationSource(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static ValuationSource FromValue(string value) =>
        All.TryGetValue(value, out var item)
            ? item
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown valuation source.");

    public override string ToString() => Value;
}
