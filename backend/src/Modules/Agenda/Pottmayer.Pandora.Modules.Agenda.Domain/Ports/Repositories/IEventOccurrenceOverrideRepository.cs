using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;

public interface IEventOccurrenceOverrideRepository : IStandardRepository<EventOccurrenceOverride, Guid>
{
    /// <summary>Every override of one event.</summary>
    Task<IReadOnlyList<EventOccurrenceOverride>> GetByEventAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Every override for a set of events — the range query loads them in one shot.</summary>
    Task<IReadOnlyList<EventOccurrenceOverride>> GetByEventsAsync(
        IReadOnlyCollection<Guid> eventIds, CancellationToken ct = default);

    /// <summary>The override for one specific occurrence, or null. The natural key is <c>(event, original start)</c>.</summary>
    Task<EventOccurrenceOverride?> FindAsync(
        Guid eventId, DateTimeOffset originalStartsAt, CancellationToken ct = default);
}
