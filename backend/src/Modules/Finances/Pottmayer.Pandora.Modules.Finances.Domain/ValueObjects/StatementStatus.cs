using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

public sealed class StatementStatus : IDomainValue<StatementStatus>
{
    public static readonly StatementStatus Open = new("open");
    public static readonly StatementStatus Closed = new("closed");
    public static readonly StatementStatus PartiallyPaid = new("partially-paid");
    public static readonly StatementStatus Paid = new("paid");
    public static readonly StatementStatus Overdue = new("overdue");

    private static readonly Dictionary<string, StatementStatus> All = new()
    {
        [Open.Value] = Open,
        [Closed.Value] = Closed,
        [PartiallyPaid.Value] = PartiallyPaid,
        [Paid.Value] = Paid,
        [Overdue.Value] = Overdue
    };

    public string Value { get; }

    private StatementStatus(string value)
    {
        Value = value;
    }

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static StatementStatus FromValue(string value) =>
        All.TryGetValue(value, out var status)
            ? status
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown statement status.");

    public override string ToString() => Value;
}
