using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.Repositories;

public sealed class ReminderDispatchRepository(IDataContextAccessor accessor)
    : StandardRepository<ReminderDispatch, Guid>(accessor), IReminderDispatchRepository
{
    public async Task<IReadOnlyList<DateTimeOffset>> GetDispatchedOccurrencesAsync(
        Guid reminderId, IReadOnlyCollection<DateTimeOffset> occurrences, CancellationToken ct = default)
    {
        if (occurrences.Count == 0)
            return [];

        return await Queryable()
            .Where(d => d.ReminderId == reminderId && occurrences.Contains(d.OccurrenceStartsAt))
            .Select(d => d.OccurrenceStartsAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReminderDispatch>> GetSnoozeDueAsync(DateTimeOffset now, int batchSize, CancellationToken ct = default) =>
        await Queryable()
            .Where(d => d.AcknowledgedAt == null && d.SnoozedUntil != null && d.SnoozedUntil <= now)
            .OrderBy(d => d.SnoozedUntil)
            .Take(batchSize)
            .ToListAsync(ct);

    public Task<ReminderDispatch?> FindAsync(
        Guid userId, Guid reminderId, DateTimeOffset occurrenceStartsAt, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(
            d => d.UserId == userId && d.ReminderId == reminderId && d.OccurrenceStartsAt == occurrenceStartsAt, ct);
}
