using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.Repositories;

public sealed class ReminderRepository(IDataContextAccessor accessor)
    : StandardRepository<Reminder, Guid>(accessor), IReminderRepository
{
    public async Task<IReadOnlyList<Reminder>> GetDueAsync(DateTimeOffset now, int batchSize, CancellationToken ct = default)
    {
        // Single-shot only (rrule null): recurring reminders are driven by the dispatch ledger, not
        // the status guard. Effective time is snoozed_until when set, else remind_at.
        var due = await Queryable()
            .Where(r => r.Rrule == null
                        && (r.Status == ReminderStatus.Scheduled || r.Status == ReminderStatus.Snoozed)
                        && (r.SnoozedUntil ?? r.RemindAt) <= now)
            .OrderBy(r => r.SnoozedUntil ?? r.RemindAt)
            .Take(batchSize)
            .ToListAsync(ct);

        return due;
    }

    public async Task<IReadOnlyList<Reminder>> GetActiveRecurringAsync(DateTimeOffset windowStart, int batchSize, CancellationToken ct = default)
    {
        // Recurring rows whose series is still live: not cancelled, and either open-ended
        // (recurrence_ends_at null) or ending on/after the window start.
        var active = await Queryable()
            .Where(r => r.Rrule != null
                        && r.Status != ReminderStatus.Cancelled
                        && (r.RecurrenceEndsAt == null || r.RecurrenceEndsAt >= windowStart))
            .OrderBy(r => r.RemindAt)
            .Take(batchSize)
            .ToListAsync(ct);

        return active;
    }

    public async Task<IReadOnlyList<Reminder>> GetByUserAsync(Guid userId, CancellationToken ct = default) =>
        await Queryable()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.RemindAt)
            .ToListAsync(ct);

    public Task<Reminder?> FindAsync(Guid userId, Guid reminderId, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(r => r.UserId == userId && r.Id == reminderId, ct);
}
