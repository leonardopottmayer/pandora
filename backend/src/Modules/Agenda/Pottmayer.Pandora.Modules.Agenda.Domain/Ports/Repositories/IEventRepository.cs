using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;

public interface IEventRepository : IStandardRepository<Event, Guid>
{
    /// <summary>One live (not soft-deleted) event scoped to its owner.</summary>
    Task<Event?> FindAsync(Guid userId, Guid eventId, CancellationToken ct = default);

    /// <summary>Whether a calendar still has any live event (the delete guard).</summary>
    Task<bool> HasLiveEventsAsync(Guid userId, Guid calendarId, CancellationToken ct = default);

    /// <summary>
    /// Live events of the user whose series could overlap <c>[from, to]</c> — the range-query scan root.
    /// A single event starting after <paramref name="to"/> or a bounded series that ended before
    /// <paramref name="from"/> is excluded; the caller expands the rest in memory.
    /// </summary>
    Task<IReadOnlyList<Event>> GetOverlappingAsync(
        Guid userId, IReadOnlyCollection<Guid>? calendarIds, DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default);

    /// <summary>
    /// Live, recurring or single events by id, across users — the sweep resolves the subjects of the
    /// event alerts it is firing. A deleted or cancelled event is simply absent.
    /// </summary>
    Task<IReadOnlyList<Event>> GetLiveByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}
