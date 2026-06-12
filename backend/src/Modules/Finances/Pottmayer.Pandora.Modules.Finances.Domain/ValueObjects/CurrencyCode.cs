using System.Text.RegularExpressions;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>
/// Currency of an account: an ISO 4217 fiat code (BRL, USD, EUR…) or a crypto ticker (BTC, ETH,
/// USDT…). Normalised to upper-case. Validated by shape (3–10 letters) rather than a closed list,
/// so new fiat/crypto codes don't require a code change.
/// </summary>
public sealed partial record CurrencyCode
{
    public string Value { get; }

    private CurrencyCode(string value) => Value = value;

    public static bool TryCreate(string? raw, out CurrencyCode? currency)
    {
        currency = null;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var normalized = raw.Trim().ToUpperInvariant();
        if (!CodePattern().IsMatch(normalized)) return false;

        currency = new CurrencyCode(normalized);
        return true;
    }

    public static CurrencyCode Create(string raw)
    {
        if (!TryCreate(raw, out var currency))
            throw new ArgumentException($"'{raw}' is not a valid currency code.", nameof(raw));
        return currency!;
    }

    public static bool IsValid(string? value) => TryCreate(value, out _);

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z]{3,10}$")]
    private static partial Regex CodePattern();
}
