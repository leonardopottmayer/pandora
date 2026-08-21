using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;

public interface IReminderRepository : IStandardRepository<Reminder, Guid>
{
    /// <summary>
    /// Single-shot reminders due to fire now (rrule null, scheduled/snoozed with effective time
    /// &lt;= now), oldest first. Recurring reminders are driven by the ledger, not this status path.
    /// </summary>
    Task<IReadOnlyList<Reminder>> GetDueAsync(DateTimeOffset now, int batchSize, CancellationToken ct = default);

    /// <summary>
    /// Recurring reminders whose series is still live at <paramref name="windowStart"/>: rrule set, not
    /// cancelled, and either open-ended or ending on/after the window start. The sweep expands each.
    /// </summary>
    Task<IReadOnlyList<Reminder>> GetActiveRecurringAsync(DateTimeOffset windowStart, int batchSize, CancellationToken ct = default);

    /// <summary>The user's reminders, newest remind time first, for the list screen.</summary>
    Task<IReadOnlyList<Reminder>> GetByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>One reminder scoped to its owner, so a user can only act on their own.</summary>
    Task<Reminder?> FindAsync(Guid userId, Guid reminderId, CancellationToken ct = default);
}
