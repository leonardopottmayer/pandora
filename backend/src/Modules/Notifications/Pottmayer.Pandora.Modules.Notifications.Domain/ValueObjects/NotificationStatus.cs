namespace Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// Lifecycle state of a <see cref="Aggregates.Notification"/> in the durable queue.
/// </summary>
public enum NotificationStatus
{
    /// <summary>Enqueued, waiting for its first dispatch attempt.</summary>
    Pending,

    /// <summary>Currently being handed to the provider.</summary>
    Sending,

    /// <summary>Accepted by the provider. Terminal.</summary>
    Sent,

    /// <summary>A dispatch attempt failed; will be retried after <c>NextAttemptAt</c>.</summary>
    Failed,

    /// <summary>Exhausted its attempts. Terminal (dead-letter).</summary>
    Dead
}
