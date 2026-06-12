using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

/// <summary>
/// Append-only audit store (fin016). Acquired from the unit-of-work context so a recorded event
/// commits in the same database transaction as the mutation it describes. Writes go through the
/// inherited <c>AddAsync</c>; the query methods back the audit timeline endpoint.
/// </summary>
public interface IAuditEventRepository : IStandardRepository<AuditEvent, Guid>
{
    /// <summary>Events for a single entity, newest first.</summary>
    Task<IReadOnlyList<AuditEvent>> GetByEntityAsync(
        Guid userId, string entityType, Guid entityId, int skip, int take, CancellationToken ct = default);

    /// <summary>Every event sharing a correlation id (e.g. a whole operation), oldest first.</summary>
    Task<IReadOnlyList<AuditEvent>> GetByCorrelationAsync(
        Guid userId, Guid correlationId, int skip, int take, CancellationToken ct = default);
}
