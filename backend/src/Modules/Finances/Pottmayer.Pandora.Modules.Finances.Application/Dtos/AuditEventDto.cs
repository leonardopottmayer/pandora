using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

public sealed record AuditEventDto(
    Guid Id,
    Guid? ActorUserId,
    string EntityType,
    Guid EntityId,
    string EventType,
    string? Data,
    Guid? CorrelationId,
    DateTimeOffset OccurredAt)
{
    public static AuditEventDto From(AuditEvent e) =>
        new(e.Id, e.ActorUserId, e.EntityType, e.EntityId, e.EventType, e.Data, e.CorrelationId, e.OccurredAt);
}
