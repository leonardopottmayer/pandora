using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;

public interface ITaskListRepository : IStandardRepository<TaskList, Guid>
{
    /// <summary>The user's task lists, by manual position then name, for the sidebar.</summary>
    Task<IReadOnlyList<TaskList>> GetByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>One list scoped to its owner.</summary>
    Task<TaskList?> FindAsync(Guid userId, Guid listId, CancellationToken ct = default);
}
