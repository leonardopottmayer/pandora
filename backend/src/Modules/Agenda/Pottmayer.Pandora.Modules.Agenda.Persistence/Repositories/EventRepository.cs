using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.Repositories;

public sealed class EventRepository(IDataContextAccessor accessor)
    : StandardRepository<Event, Guid>(accessor), IEventRepository
{
    public Task<Event?> FindAsync(Guid userId, Guid eventId, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(e => e.UserId == userId && e.Id == eventId && e.DeletedAt == null, ct);

    public Task<bool> HasLiveEventsAsync(Guid userId, Guid calendarId, CancellationToken ct = default) =>
        Queryable().AnyAsync(e => e.UserId == userId && e.CalendarId == calendarId && e.DeletedAt == null, ct);

    public async Task<IReadOnlyList<Event>> GetOverlappingAsync(
        Guid userId, IReadOnlyCollection<Guid>? calendarIds, DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default)
    {
        var query = Queryable()
            .Where(e => e.UserId == userId && e.DeletedAt == null && e.StartsAt <= to)
            // A single event still runs into the window; a recurring one is open-ended or ends on/after it.
            .Where(e => (e.Rrule == null && e.EndsAt >= from)
                        || (e.Rrule != null && (e.RecurrenceEndsAt == null || e.RecurrenceEndsAt >= from)));

        if (calendarIds is { Count: > 0 })
            query = query.Where(e => calendarIds.Contains(e.CalendarId));

        return await query.OrderBy(e => e.StartsAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Event>> GetLiveByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return [];

        return await Queryable()
            .Where(e => ids.Contains(e.Id) && e.DeletedAt == null && e.Status != EventStatus.Cancelled)
            .ToListAsync(ct);
    }
}
