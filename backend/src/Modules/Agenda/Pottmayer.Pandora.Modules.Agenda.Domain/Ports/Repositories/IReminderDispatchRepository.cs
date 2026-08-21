using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;

/// <summary>The per-occurrence dispatch ledger. Its unique <c>(reminder, occurrence)</c> key is the sweep's idempotency guard.</summary>
public interface IReminderDispatchRepository : IStandardRepository<ReminderDispatch, Guid>
{
    /// <summary>
    /// The occurrence anchors already dispatched for a reminder among <paramref name="occurrences"/>,
    /// so the sweep can fire only the ones without a ledger row.
    /// </summary>
    Task<IReadOnlyList<DateTimeOffset>> GetDispatchedOccurrencesAsync(
        Guid reminderId, IReadOnlyCollection<DateTimeOffset> occurrences, CancellationToken ct = default);

    /// <summary>Dispatch rows whose snooze has passed and are not acknowledged, oldest first — the re-fire batch.</summary>
    Task<IReadOnlyList<ReminderDispatch>> GetSnoozeDueAsync(DateTimeOffset now, int batchSize, CancellationToken ct = default);

    /// <summary>One occurrence's dispatch row, scoped to its owner, for acknowledge/snooze.</summary>
    Task<ReminderDispatch?> FindAsync(
        Guid userId, Guid reminderId, DateTimeOffset occurrenceStartsAt, CancellationToken ct = default);
}
