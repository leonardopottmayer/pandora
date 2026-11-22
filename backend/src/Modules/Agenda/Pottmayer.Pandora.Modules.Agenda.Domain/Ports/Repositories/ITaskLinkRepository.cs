using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;

public interface ITaskLinkRepository : IStandardRepository<TaskLink, Guid>
{
    Task<IReadOnlyList<TaskLink>> GetByBindingAsync(Guid bindingId, CancellationToken ct = default);

    Task<TaskLink?> FindByTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>The links whose tasks Pandora changed and Google does not have yet, oldest change first.</summary>
    Task<IReadOnlyList<TaskLink>> GetPendingAsync(Guid bindingId, CancellationToken ct = default);
}
