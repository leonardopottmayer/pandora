using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>
/// Lifecycle of a ledger entry. <see cref="Pending"/> is scheduled/future and does not affect the
/// posted balance; <see cref="Posted"/> is effective; <see cref="Void"/> is a terminal cancellation
/// (a posted entry is never physically deleted).
/// </summary>
public sealed class TransactionStatus : IDomainValue<TransactionStatus>
{
    public static readonly TransactionStatus Pending = new("pending");
    public static readonly TransactionStatus Posted = new("posted");
    public static readonly TransactionStatus Void = new("void");

    public string Value { get; }

    private TransactionStatus(string value) => Value = value;

    public static bool IsSupported(string? value) => value is "pending" or "posted" or "void";

    public static TransactionStatus FromValue(string value) => value switch
    {
        "pending" => Pending,
        "posted" => Posted,
        "void" => Void,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown transaction status.")
    };

    public override string ToString() => Value;
}
