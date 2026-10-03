using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>What a file attached to a transaction is (fin017): the bill to pay, the proof it was paid, the invoice.</summary>
public sealed class AttachmentKind : IDomainValue<AttachmentKind>
{
    /// <summary>The bill to pay (a boleto, a utility bill).</summary>
    public static readonly AttachmentKind Bill = new("bill");

    /// <summary>Proof of payment (a Pix or boleto receipt, a card slip).</summary>
    public static readonly AttachmentKind Receipt = new("receipt");

    /// <summary>The invoice for what was bought (a nota fiscal).</summary>
    public static readonly AttachmentKind Invoice = new("invoice");

    public static readonly AttachmentKind Other = new("other");

    private static readonly Dictionary<string, AttachmentKind> All = new()
    {
        [Bill.Value] = Bill,
        [Receipt.Value] = Receipt,
        [Invoice.Value] = Invoice,
        [Other.Value] = Other
    };

    public string Value { get; }

    private AttachmentKind(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static AttachmentKind FromValue(string value) =>
        All.TryGetValue(value, out var kind)
            ? kind
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown attachment kind.");

    public override string ToString() => Value;
}
