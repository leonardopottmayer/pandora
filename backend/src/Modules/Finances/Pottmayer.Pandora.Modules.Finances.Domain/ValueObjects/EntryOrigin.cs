using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>
/// Provenance of an entry created in the module: typed by the user (<see cref="Manual"/>), produced
/// from an <see cref="Aggregates.ImportFile"/> (<see cref="Import"/>), generated from a
/// <see cref="Aggregates.RecurringTransaction"/> (<see cref="Recurrence"/>), a future-dated estimate
/// not yet committed (<see cref="Projection"/>), or the counter-entry of a voided one
/// (<see cref="Reversal"/>). Shared between <see cref="Aggregates.Transaction"/>,
/// <see cref="Aggregates.InstallmentPlan"/> and <see cref="Aggregates.PendingTransaction"/>, each of
/// which only ever produces a subset of these values.
/// </summary>
public sealed class EntryOrigin : IDomainValue<EntryOrigin>
{
    public static readonly EntryOrigin Manual = new("manual");
    public static readonly EntryOrigin Import = new("import");
    public static readonly EntryOrigin Recurrence = new("recurrence");
    public static readonly EntryOrigin Projection = new("projection");
    public static readonly EntryOrigin Reversal = new("reversal");

    private static readonly Dictionary<string, EntryOrigin> All = new()
    {
        [Manual.Value] = Manual,
        [Import.Value] = Import,
        [Recurrence.Value] = Recurrence,
        [Projection.Value] = Projection,
        [Reversal.Value] = Reversal
    };

    public string Value { get; }

    private EntryOrigin(string value) => Value = value;

    public static bool IsSupported(string? value) => value is not null && All.ContainsKey(value);

    public static EntryOrigin FromValue(string value) =>
        All.TryGetValue(value, out var origin)
            ? origin
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown entry origin.");

    public override string ToString() => Value;
}
