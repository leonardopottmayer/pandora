using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>How a position of an asset class works: <c>unit</c> keeps a quantity and an average cost; <c>application</c> is fixed income, one position per application.</summary>
public sealed class AssetBehavior : IDomainValue<AssetBehavior>
{
    public static readonly AssetBehavior Unit = new("unit");
    public static readonly AssetBehavior Application = new("application");

    private static readonly Dictionary<string, AssetBehavior> All = new()
    {
        [Unit.Value] = Unit,
        [Application.Value] = Application
    };

    public string Value { get; }

    private AssetBehavior(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static AssetBehavior FromValue(string value) =>
        All.TryGetValue(value, out var item)
            ? item
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown asset behavior.");

    public override string ToString() => Value;
}
