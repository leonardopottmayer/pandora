using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;

public interface ICalendarBindingRepository : IStandardRepository<CalendarBinding, Guid>
{
    Task<CalendarBinding?> FindAsync(Guid userId, Guid bindingId, CancellationToken ct = default);
    Task<CalendarBinding?> GetAsync(Guid bindingId, CancellationToken ct = default);
    Task<IReadOnlyList<CalendarBinding>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<CalendarBinding>> GetByAccountAsync(Guid externalAccountId, CancellationToken ct = default);

    /// <summary>The bindings the job walks: every one in <c>ok</c>.</summary>
    Task<IReadOnlyList<Guid>> ListSyncableIdsAsync(CancellationToken ct = default);

    Task<bool> IsCalendarBoundAsync(Guid calendarId, CancellationToken ct = default);
}
