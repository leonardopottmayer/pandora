using System.Text.Json;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;

namespace Pottmayer.Pandora.Modules.Finances.Application.Auditing;

/// <summary>
/// Records audit events through the <see cref="IAuditEventRepository"/> acquired from the current
/// unit-of-work context, so each event commits in the same transaction as the mutation it
/// describes. Every Finances mutation is expected to call one of these from inside
/// <c>IUnitOfWorkFactory.ExecuteAsync</c>.
/// </summary>
public static class AuditTrailExtensions
{
    public static Task RecordAsync(
        this IDataContext context,
        Guid userId,
        Guid? actorUserId,
        string entityType,
        Guid entityId,
        string eventType,
        DateTimeOffset occurredAt,
        object? data = null,
        Guid? correlationId = null,
        CancellationToken ct = default)
    {
        var json = data is null ? null : JsonSerializer.Serialize(data);
        var auditEvent = AuditEvent.Record(
            userId, actorUserId, entityType, entityId, eventType, json, correlationId, occurredAt);
        return context.AcquireRepository<IAuditEventRepository>().AddAsync(auditEvent, ct);
    }
}
