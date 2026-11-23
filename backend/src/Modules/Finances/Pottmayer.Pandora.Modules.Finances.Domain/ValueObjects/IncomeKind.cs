using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>The kind of income a position paid.</summary>
public sealed class IncomeKind : IDomainValue<IncomeKind>
{
    public static readonly IncomeKind Dividend = new("dividend");
    public static readonly IncomeKind InterestOnEquity = new("interest-on-equity");
    public static readonly IncomeKind Rent = new("rent");
    public static readonly IncomeKind Coupon = new("coupon");
    public static readonly IncomeKind Other = new("other");

    private static readonly Dictionary<string, IncomeKind> All = new()
    {
        [Dividend.Value] = Dividend,
        [InterestOnEquity.Value] = InterestOnEquity,
        [Rent.Value] = Rent,
        [Coupon.Value] = Coupon,
        [Other.Value] = Other
    };

    public string Value { get; }

    private IncomeKind(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static IncomeKind FromValue(string value) =>
        All.TryGetValue(value, out var item)
            ? item
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown income kind.");

    public override string ToString() => Value;
}
