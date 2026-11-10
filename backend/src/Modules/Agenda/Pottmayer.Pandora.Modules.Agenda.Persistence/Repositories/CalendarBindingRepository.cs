using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.Repositories;

public sealed class CalendarBindingRepository(IDataContextAccessor accessor)
    : StandardRepository<CalendarBinding, Guid>(accessor), ICalendarBindingRepository
{
    public Task<CalendarBinding?> FindAsync(Guid userId, Guid bindingId, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(b => b.UserId == userId && b.Id == bindingId, ct);

    public Task<CalendarBinding?> GetAsync(Guid bindingId, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(b => b.Id == bindingId, ct);

    public async Task<IReadOnlyList<CalendarBinding>> GetByUserAsync(Guid userId, CancellationToken ct = default) =>
        await Queryable().Where(b => b.UserId == userId).OrderBy(b => b.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<CalendarBinding>> GetByAccountAsync(Guid externalAccountId, CancellationToken ct = default) =>
        await Queryable().Where(b => b.ExternalAccountId == externalAccountId).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> ListSyncableIdsAsync(CancellationToken ct = default) =>
        await Queryable().Where(b => b.Status == CalendarBindingStatus.Ok).Select(b => b.Id).ToListAsync(ct);

    public Task<bool> IsCalendarBoundAsync(Guid calendarId, CancellationToken ct = default) =>
        Queryable().AnyAsync(b => b.CalendarId == calendarId, ct);
}
