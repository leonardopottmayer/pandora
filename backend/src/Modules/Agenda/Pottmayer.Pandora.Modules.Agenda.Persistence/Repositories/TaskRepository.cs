using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.Repositories;

public sealed class TaskRepository(IDataContextAccessor accessor)
    : StandardRepository<TaskItem, Guid>(accessor), ITaskRepository
{
    public Task<TaskItem?> FindAsync(Guid userId, Guid taskId, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(t => t.UserId == userId && t.Id == taskId && t.DeletedAt == null, ct);

    public async Task<IReadOnlyList<TaskItem>> GetByUserAsync(
        Guid userId, Guid? listId, TaskItemStatus? status, CancellationToken ct = default)
    {
        var query = Queryable().Where(t => t.UserId == userId && t.DeletedAt == null);

        if (listId is { } list)
            query = query.Where(t => t.ListId == list);
        if (status is { } s)
            query = query.Where(t => t.Status == s);

        return await query
            .OrderBy(t => t.Position)
            .ThenBy(t => t.DueAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskItem>> GetLiveDueByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return [];

        return await Queryable()
            .Where(t => ids.Contains(t.Id)
                        && t.DeletedAt == null
                        && t.DueAt != null
                        && (t.Status == TaskItemStatus.Todo || t.Status == TaskItemStatus.InProgress))
            .ToListAsync(ct);
    }
}
