using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>Cadence of a <see cref="RecurrenceRule"/> / <see cref="Aggregates.RecurringTransaction"/>.</summary>
public sealed class RecurrenceFrequency : IDomainValue<RecurrenceFrequency>
{
    public static readonly RecurrenceFrequency Daily = new("daily");
    public static readonly RecurrenceFrequency Weekly = new("weekly");
    public static readonly RecurrenceFrequency Monthly = new("monthly");
    public static readonly RecurrenceFrequency Yearly = new("yearly");

    private static readonly Dictionary<string, RecurrenceFrequency> All = new()
    {
        [Daily.Value] = Daily,
        [Weekly.Value] = Weekly,
        [Monthly.Value] = Monthly,
        [Yearly.Value] = Yearly
    };

    public string Value { get; }

    private RecurrenceFrequency(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static RecurrenceFrequency FromValue(string value) =>
        All.TryGetValue(value, out var frequency)
            ? frequency
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown recurrence frequency.");

    public override string ToString() => Value;
}
