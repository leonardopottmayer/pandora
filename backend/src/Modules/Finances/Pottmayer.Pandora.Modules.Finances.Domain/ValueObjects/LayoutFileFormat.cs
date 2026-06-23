using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>File format an <see cref="Aggregates.ImportLayout"/> knows how to parse.</summary>
public sealed class LayoutFileFormat : IDomainValue<LayoutFileFormat>
{
    public static readonly LayoutFileFormat Ofx = new("ofx");
    public static readonly LayoutFileFormat Csv = new("csv");

    private static readonly Dictionary<string, LayoutFileFormat> All = new()
    {
        [Ofx.Value] = Ofx,
        [Csv.Value] = Csv
    };

    public string Value { get; }

    private LayoutFileFormat(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static LayoutFileFormat FromValue(string value) =>
        All.TryGetValue(value, out var format)
            ? format
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown file format.");

    public override string ToString() => Value;
}
