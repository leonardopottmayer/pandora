using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;

public interface IEventLinkRepository : IStandardRepository<EventLink, Guid>
{
    Task<IReadOnlyList<EventLink>> GetByBindingAsync(Guid bindingId, CancellationToken ct = default);
}
