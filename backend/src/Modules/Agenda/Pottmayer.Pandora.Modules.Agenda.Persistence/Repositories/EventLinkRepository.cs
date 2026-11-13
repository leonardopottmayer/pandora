using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.Repositories;

public sealed class EventLinkRepository(IDataContextAccessor accessor)
    : StandardRepository<EventLink, Guid>(accessor), IEventLinkRepository
{
    public async Task<IReadOnlyList<EventLink>> GetByBindingAsync(Guid bindingId, CancellationToken ct = default) =>
        await Queryable().Where(l => l.BindingId == bindingId).ToListAsync(ct);

    public Task<EventLink?> FindByEventAsync(Guid eventId, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(l => l.EventId == eventId, ct);

    public async Task<IReadOnlyList<EventLink>> GetPendingAsync(Guid bindingId, CancellationToken ct = default) =>
        await Queryable().Where(l => l.BindingId == bindingId && l.PendingPushAt != null)
            .OrderBy(l => l.PendingPushAt).ToListAsync(ct);
}
