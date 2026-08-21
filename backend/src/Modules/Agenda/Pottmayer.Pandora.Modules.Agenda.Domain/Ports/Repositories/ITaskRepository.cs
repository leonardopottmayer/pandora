using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;

public interface ITaskRepository : IStandardRepository<TaskItem, Guid>
{
    /// <summary>One live (not soft-deleted) task scoped to its owner.</summary>
    Task<TaskItem?> FindAsync(Guid userId, Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// The user's live tasks, optionally filtered by list and status, ordered by position then due date.
    /// </summary>
    Task<IReadOnlyList<TaskItem>> GetByUserAsync(
        Guid userId, Guid? listId, TaskItemStatus? status, CancellationToken ct = default);

    /// <summary>
    /// Live, due-dated tasks by id, across users — the sweep resolves the subjects of the task alerts it
    /// is firing. A task that is done, cancelled, deleted or has no due date is simply absent.
    /// </summary>
    Task<IReadOnlyList<TaskItem>> GetLiveDueByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}
