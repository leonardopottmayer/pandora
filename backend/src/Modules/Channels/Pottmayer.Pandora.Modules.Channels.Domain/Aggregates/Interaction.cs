using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;

/// <summary>
/// A registered inline button and the route it takes back. It exists so a Telegram callback — capped
/// at 64 bytes — can carry just this row's id and still resolve to a full (user, module, action,
/// payload). Single use: a second tap resolves to an already-consumed row and is answered as expired.
/// </summary>
public sealed class Interaction : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }

    /// <summary>The module that declared the button; the routing key is built from it. Opaque here.</summary>
    public string OwnerModule { get; private set; } = string.Empty;

    /// <summary>The action the owner assigned, e.g. <c>task_done</c>. Opaque to this module.</summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>Owner-defined JSON, returned intact. Null when the button needs none.</summary>
    public string? Payload { get; private set; }

    /// <summary>The queued notification that declared the button. Null for system-message buttons.</summary>
    public Guid? NotificationId { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private Interaction() { }

    public static Interaction Register(
        Guid userId,
        string ownerModule,
        string action,
        string? payload,
        Guid? notificationId,
        DateTimeOffset expiresAt,
        TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            OwnerModule = ownerModule,
            Action = action,
            Payload = payload,
            NotificationId = notificationId,
            ExpiresAt = expiresAt,
            CreatedAt = timeProvider.GetUtcNow()
        };

    public bool IsUsable(DateTimeOffset now) => ConsumedAt is null && ExpiresAt > now;

    /// <summary>Burns the button. A second click is an expired no-op, not a second command.</summary>
    public void Consume(TimeProvider timeProvider) => ConsumedAt = timeProvider.GetUtcNow();
}
