using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>
/// Which kind of destination an <see cref="Aggregates.ImportLayout"/> targets: a regular
/// <see cref="Account"/> or a <see cref="Aggregates.Card"/> statement. Distinct from
/// <see cref="AccountType"/> (the account's own nature, e.g. checking/savings) — this is only the
/// two-way split the import pipeline needs to pick a parser.
/// </summary>
public sealed class ImportLayoutAccountType : IDomainValue<ImportLayoutAccountType>
{
    public static readonly ImportLayoutAccountType Account = new("account");
    public static readonly ImportLayoutAccountType Card = new("card");

    private static readonly Dictionary<string, ImportLayoutAccountType> All = new()
    {
        [Account.Value] = Account,
        [Card.Value] = Card
    };

    public string Value { get; }

    private ImportLayoutAccountType(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static ImportLayoutAccountType FromValue(string value) =>
        All.TryGetValue(value, out var type)
            ? type
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown import layout account type.");

    public override string ToString() => Value;
}
