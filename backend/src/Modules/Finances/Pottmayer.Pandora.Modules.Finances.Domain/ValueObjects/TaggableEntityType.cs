using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>
/// Kind of entity a <c>TagLink</c> can point at (fin005, polymorphic). The enum is complete from the
/// start: <see cref="RecurringTransaction"/> and <see cref="PendingTransaction"/> are accepted values
/// even though those aggregates only arrive in phase 08 — linking to one simply fails the existence
/// check until then.
/// </summary>
public sealed class TaggableEntityType : IDomainValue<TaggableEntityType>
{
    public static readonly TaggableEntityType Account = new("account");
    public static readonly TaggableEntityType Card = new("card");
    public static readonly TaggableEntityType CardStatement = new("card_statement");
    public static readonly TaggableEntityType Transaction = new("transaction");
    public static readonly TaggableEntityType RecurringTransaction = new("recurring_transaction");
    public static readonly TaggableEntityType PendingTransaction = new("pending_transaction");

    private static readonly Dictionary<string, TaggableEntityType> All = new()
    {
        [Account.Value] = Account,
        [Card.Value] = Card,
        [CardStatement.Value] = CardStatement,
        [Transaction.Value] = Transaction,
        [RecurringTransaction.Value] = RecurringTransaction,
        [PendingTransaction.Value] = PendingTransaction
    };

    public string Value { get; }

    private TaggableEntityType(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static TaggableEntityType FromValue(string value) =>
        All.TryGetValue(value, out var type)
            ? type
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown taggable entity type.");

    public override string ToString() => Value;
}
