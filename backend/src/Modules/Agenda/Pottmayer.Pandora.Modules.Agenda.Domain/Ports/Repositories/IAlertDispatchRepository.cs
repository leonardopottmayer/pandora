using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;

/// <summary>The alert dispatch ledger (agd008). Its unique <c>(alert, occurrence)</c> key is the sweep's idempotency guard.</summary>
public interface IAlertDispatchRepository : IStandardRepository<AlertDispatch, Guid>
{
    /// <summary>Whether an alert has already been dispatched for a given anchor, so the sweep fires it at most once.</summary>
    Task<bool> ExistsAsync(Guid alertId, DateTimeOffset occurrenceStartsAt, CancellationToken ct = default);
}
