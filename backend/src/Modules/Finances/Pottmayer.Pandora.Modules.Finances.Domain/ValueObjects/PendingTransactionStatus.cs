using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>
/// Decision state of a suggestion waiting for user review: <see cref="Pending"/> until the user
/// acts, then terminally <see cref="Approved"/> (a transaction was created or matched) or
/// <see cref="Rejected"/>.
/// </summary>
public sealed class PendingTransactionStatus : IDomainValue<PendingTransactionStatus>
{
    public static readonly PendingTransactionStatus Pending = new("pending");
    public static readonly PendingTransactionStatus Approved = new("approved");
    public static readonly PendingTransactionStatus Rejected = new("rejected");

    private static readonly Dictionary<string, PendingTransactionStatus> All = new()
    {
        [Pending.Value] = Pending,
        [Approved.Value] = Approved,
        [Rejected.Value] = Rejected
    };

    public string Value { get; }

    private PendingTransactionStatus(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static PendingTransactionStatus FromValue(string value) =>
        All.TryGetValue(value, out var status)
            ? status
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown pending transaction status.");

    public override string ToString() => Value;
}
