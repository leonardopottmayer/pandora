using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>
/// A supported financial institution, keyed by its Brazilian COMPE code. Linking an
/// <see cref="Aggregates.Account"/> or <see cref="Aggregates.Card"/> to a bank lets the import
/// pipeline pick the right <see cref="Aggregates.ImportLayout"/> by (bank, format, account/card)
/// instead of sniffing the file's content. Codes are matched tolerating leading zeros, since some
/// exports drop them (e.g. "77" and "077" both resolve to Banco Inter).
/// </summary>
public sealed class Bank : IDomainValue<Bank>
{
    public static readonly Bank Inter = new("077", "Banco Inter");
    public static readonly Bank Viacredi = new("085", "Viacredi");
    public static readonly Bank Nubank = new("260", "Nubank");
    public static readonly Bank Itau = new("341", "Itaú");

    private static readonly Dictionary<string, Bank> ByCode =
        new[] { Inter, Viacredi, Nubank, Itau }.ToDictionary(b => Normalize(b.Value));

    /// <summary>Canonical COMPE code (e.g. "077").</summary>
    public string Value { get; }
    public string Name { get; }

    private Bank(string value, string name)
    {
        Value = value;
        Name = name;
    }

    public static IReadOnlyCollection<Bank> All => ByCode.Values;

    public static bool IsSupported(string? value) =>
        value is not null && ByCode.ContainsKey(Normalize(value));

    public static Bank FromValue(string value) =>
        ByCode.TryGetValue(Normalize(value), out var bank)
            ? bank
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown bank code.");

    public static Bank? TryFromValue(string? value) =>
        value is not null && ByCode.TryGetValue(Normalize(value), out var bank) ? bank : null;

    /// <summary>Normalizes a code to canonical form (e.g. "77" → "077"), or null when unsupported.</summary>
    public static string? Canonicalize(string? value) => TryFromValue(value)?.Value;

    /// <summary>True when two codes refer to the same bank, tolerating leading-zero differences.</summary>
    public static bool Matches(string? left, string? right) =>
        left is not null && right is not null && Normalize(left) == Normalize(right)
        && ByCode.ContainsKey(Normalize(left));

    public override string ToString() => Value;

    private static string Normalize(string value)
    {
        var trimmed = value.Trim().TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }
}
