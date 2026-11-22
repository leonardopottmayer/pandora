using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.Repositories;

public sealed class EventOccurrenceOverrideRepository(IDataContextAccessor accessor)
    : StandardRepository<EventOccurrenceOverride, Guid>(accessor), IEventOccurrenceOverrideRepository
{
    public async Task<IReadOnlyList<EventOccurrenceOverride>> GetByEventAsync(Guid eventId, CancellationToken ct = default) =>
        await Queryable()
            .Where(o => o.EventId == eventId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<EventOccurrenceOverride>> GetByEventsAsync(
        IReadOnlyCollection<Guid> eventIds, CancellationToken ct = default)
    {
        if (eventIds.Count == 0)
            return [];

        return await Queryable()
            .Where(o => eventIds.Contains(o.EventId))
            .ToListAsync(ct);
    }

    public Task<EventOccurrenceOverride?> FindAsync(
        Guid eventId, DateTimeOffset originalStartsAt, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(o => o.EventId == eventId && o.OriginalStartsAt == originalStartsAt, ct);
}
