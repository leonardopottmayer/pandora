using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;

public interface ITaskListBindingRepository : IStandardRepository<TaskListBinding, Guid>
{
    Task<TaskListBinding?> FindAsync(Guid userId, Guid bindingId, CancellationToken ct = default);
    Task<TaskListBinding?> GetAsync(Guid bindingId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskListBinding>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskListBinding>> GetByAccountAsync(Guid externalAccountId, CancellationToken ct = default);

    /// <summary>The bindings the job walks: every one in <c>ok</c>.</summary>
    Task<IReadOnlyList<Guid>> ListSyncableIdsAsync(CancellationToken ct = default);

    Task<TaskListBinding?> FindByListAsync(Guid listId, CancellationToken ct = default);
}
