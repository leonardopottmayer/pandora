using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Persistence.Repositories;

public sealed class AlertDispatchRepository(IDataContextAccessor accessor)
    : StandardRepository<AlertDispatch, Guid>(accessor), IAlertDispatchRepository
{
    public Task<bool> ExistsAsync(Guid alertId, DateTimeOffset occurrenceStartsAt, CancellationToken ct = default) =>
        Queryable().AnyAsync(d => d.AlertId == alertId && d.OccurrenceStartsAt == occurrenceStartsAt, ct);
}
