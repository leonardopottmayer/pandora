using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;

public interface ICalendarRepository : IStandardRepository<Calendar, Guid>
{
    /// <summary>The user's calendars, by name, for the sidebar.</summary>
    Task<IReadOnlyList<Calendar>> GetByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>One calendar scoped to its owner.</summary>
    Task<Calendar?> FindAsync(Guid userId, Guid calendarId, CancellationToken ct = default);
}
