using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>What a fixed-income application is indexed to.</summary>
public sealed class FixedIncomeIndex : IDomainValue<FixedIncomeIndex>
{
    public static readonly FixedIncomeIndex Cdi = new("cdi");
    public static readonly FixedIncomeIndex Selic = new("selic");
    public static readonly FixedIncomeIndex Ipca = new("ipca");
    public static readonly FixedIncomeIndex Pre = new("pre");
    public static readonly FixedIncomeIndex Other = new("other");

    private static readonly Dictionary<string, FixedIncomeIndex> All = new()
    {
        [Cdi.Value] = Cdi,
        [Selic.Value] = Selic,
        [Ipca.Value] = Ipca,
        [Pre.Value] = Pre,
        [Other.Value] = Other
    };

    public string Value { get; }

    private FixedIncomeIndex(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static FixedIncomeIndex FromValue(string value) =>
        All.TryGetValue(value, out var item)
            ? item
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown fixed income index.");

    public override string ToString() => Value;
}
