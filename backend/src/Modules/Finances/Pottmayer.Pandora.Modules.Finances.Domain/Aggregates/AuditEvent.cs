using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// Append-only audit record. Never updated or deleted.
/// </summary>
public sealed class AuditEvent : AggregateRoot<Guid>
{
    /// <summary>Owner of the data the event concerns.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Who performed the action; <c>null</c> for system/job actors.</summary>
    public Guid? ActorUserId { get; private set; }

    /// <summary>Entity kind, e.g. <c>account</c>, <c>user-category</c>, <c>transaction</c>.</summary>
    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    /// <summary>Dotted event name, e.g. <c>account.created</c>, <c>category.updated</c>.</summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>Optional JSON detail/diff (jsonb). Shape is <c>{ field: { old, new } }</c> or free-form.</summary>
    public string? Data { get; private set; }

    /// <summary>Groups every event of a single operation (e.g. a whole import). Optional.</summary>
    public Guid? CorrelationId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    private AuditEvent() { }

    /// <summary>Records a single audit entry. Once recorded, an event is never changed.</summary>
    public static AuditEvent Record(
        Guid userId,
        Guid? actorUserId,
        string entityType,
        Guid entityId,
        string eventType,
        string? data,
        Guid? correlationId,
        DateTimeOffset occurredAt) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            ActorUserId = actorUserId,
            EntityType = entityType,
            EntityId = entityId,
            EventType = eventType,
            Data = data,
            CorrelationId = correlationId,
            OccurredAt = occurredAt
        };
}
