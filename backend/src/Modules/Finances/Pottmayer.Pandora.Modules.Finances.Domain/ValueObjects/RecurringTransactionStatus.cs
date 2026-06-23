using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>
/// Generation state of a recurring template: <see cref="Active"/> generates occurrences on
/// schedule, <see cref="Paused"/> temporarily stops it (resumable), <see cref="Finished"/> is
/// terminal (end date or max occurrences reached).
/// </summary>
public sealed class RecurringTransactionStatus : IDomainValue<RecurringTransactionStatus>
{
    public static readonly RecurringTransactionStatus Active = new("active");
    public static readonly RecurringTransactionStatus Paused = new("paused");
    public static readonly RecurringTransactionStatus Finished = new("finished");

    private static readonly Dictionary<string, RecurringTransactionStatus> All = new()
    {
        [Active.Value] = Active,
        [Paused.Value] = Paused,
        [Finished.Value] = Finished
    };

    public string Value { get; }

    private RecurringTransactionStatus(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static RecurringTransactionStatus FromValue(string value) =>
        All.TryGetValue(value, out var status)
            ? status
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown recurring transaction status.");

    public override string ToString() => Value;
}
