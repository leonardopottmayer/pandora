using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.Repositories;

public sealed class CalendarRepository(IDataContextAccessor accessor)
    : StandardRepository<Calendar, Guid>(accessor), ICalendarRepository
{
    public async Task<IReadOnlyList<Calendar>> GetByUserAsync(Guid userId, CancellationToken ct = default) =>
        await Queryable()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

    public Task<Calendar?> FindAsync(Guid userId, Guid calendarId, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(c => c.UserId == userId && c.Id == calendarId, ct);
}
