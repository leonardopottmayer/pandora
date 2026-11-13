using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;

public interface IEventLinkRepository : IStandardRepository<EventLink, Guid>
{
    Task<IReadOnlyList<EventLink>> GetByBindingAsync(Guid bindingId, CancellationToken ct = default);

    Task<EventLink?> FindByEventAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>The links whose events Pandora changed and Google does not have yet.</summary>
    Task<IReadOnlyList<EventLink>> GetPendingAsync(Guid bindingId, CancellationToken ct = default);
}
