using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.Repositories;

public sealed class TaskListRepository(IDataContextAccessor accessor)
    : StandardRepository<TaskList, Guid>(accessor), ITaskListRepository
{
    public async Task<IReadOnlyList<TaskList>> GetByUserAsync(Guid userId, CancellationToken ct = default) =>
        await Queryable()
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.Position)
            .ThenBy(l => l.Name)
            .ToListAsync(ct);

    public Task<TaskList?> FindAsync(Guid userId, Guid listId, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(l => l.UserId == userId && l.Id == listId, ct);
}
