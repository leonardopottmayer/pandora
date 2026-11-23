using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>Whether a position still holds something.</summary>
public sealed class PositionStatus : IDomainValue<PositionStatus>
{
    public static readonly PositionStatus Open = new("open");
    public static readonly PositionStatus Closed = new("closed");

    private static readonly Dictionary<string, PositionStatus> All = new()
    {
        [Open.Value] = Open,
        [Closed.Value] = Closed
    };

    public string Value { get; }

    private PositionStatus(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static PositionStatus FromValue(string value) =>
        All.TryGetValue(value, out var item)
            ? item
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown position status.");

    public override string ToString() => Value;
}
