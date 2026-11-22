using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.Repositories;

public sealed class TaskLinkRepository(IDataContextAccessor accessor)
    : StandardRepository<TaskLink, Guid>(accessor), ITaskLinkRepository
{
    public async Task<IReadOnlyList<TaskLink>> GetByBindingAsync(Guid bindingId, CancellationToken ct = default) =>
        await Queryable().Where(l => l.BindingId == bindingId).ToListAsync(ct);

    public Task<TaskLink?> FindByTaskAsync(Guid taskId, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(l => l.TaskId == taskId, ct);

    public async Task<IReadOnlyList<TaskLink>> GetPendingAsync(Guid bindingId, CancellationToken ct = default) =>
        await Queryable().Where(l => l.BindingId == bindingId && l.PendingPushAt != null)
            .OrderBy(l => l.PendingPushAt).ToListAsync(ct);
}
