using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>Whether a <c>CardNumber</c> is printed on the plastic card or only exists in the bank's app.</summary>
public sealed class CardNumberKind : IDomainValue<CardNumberKind>
{
    public static readonly CardNumberKind Physical = new("physical");
    public static readonly CardNumberKind Virtual = new("virtual");

    private static readonly Dictionary<string, CardNumberKind> All = new()
    {
        [Physical.Value] = Physical,
        [Virtual.Value] = Virtual
    };

    public string Value { get; }

    private CardNumberKind(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static CardNumberKind FromValue(string value) =>
        All.TryGetValue(value, out var kind)
            ? kind
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown card number kind.");

    public override string ToString() => Value;
}
