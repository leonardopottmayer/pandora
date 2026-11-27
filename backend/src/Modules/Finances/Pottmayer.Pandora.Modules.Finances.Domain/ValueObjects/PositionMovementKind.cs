using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>What happened to a position.</summary>
public sealed class PositionMovementKind : IDomainValue<PositionMovementKind>
{
    public static readonly PositionMovementKind Opening = new("opening");
    public static readonly PositionMovementKind Buy = new("buy");
    public static readonly PositionMovementKind Sell = new("sell");
    public static readonly PositionMovementKind Redemption = new("redemption");
    public static readonly PositionMovementKind Maturity = new("maturity");
    public static readonly PositionMovementKind Income = new("income");
    public static readonly PositionMovementKind Split = new("split");
    public static readonly PositionMovementKind ComeCotas = new("come-cotas");

    private static readonly Dictionary<string, PositionMovementKind> All = new()
    {
        [Opening.Value] = Opening,
        [Buy.Value] = Buy,
        [Sell.Value] = Sell,
        [Redemption.Value] = Redemption,
        [Maturity.Value] = Maturity,
        [Income.Value] = Income,
        [Split.Value] = Split,
        [ComeCotas.Value] = ComeCotas
    };

    public string Value { get; }

    private PositionMovementKind(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static PositionMovementKind FromValue(string value) =>
        All.TryGetValue(value, out var item)
            ? item
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown position movement kind.");

    public override string ToString() => Value;
}
