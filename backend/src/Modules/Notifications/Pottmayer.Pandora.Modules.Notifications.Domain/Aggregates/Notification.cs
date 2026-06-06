using Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Notifications.Domain.Aggregates;

public sealed class Notification : AggregateRoot<Guid>, IAuditable
{
    public const int DefaultMaxAttempts = 5;
    private const int MaxErrorLength = 1000;

    public Channel Channel { get; private set; } = Channel.Email;
    public Email Recipient { get; private set; } = null!;
    public TemplateKey TemplateKey { get; private set; } = null!;
    public string Locale { get; private set; } = "en";
    public string Payload { get; private set; } = "{}";
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public bool IsHtml { get; private set; }
    public NotificationStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public string? LastError { get; private set; }
    public string? Provider { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public Guid CorrelationId { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private Notification() { }

    /// <summary>
    /// Enqueues a notification in <see cref="NotificationStatus.Pending"/>, due immediately.
    /// </summary>
    public static Notification Queue(
        Channel channel,
        Email recipient,
        TemplateKey templateKey,
        string locale,
        string payload,
        NotificationContent content,
        Guid correlationId,
        TimeProvider timeProvider,
        int maxAttempts = DefaultMaxAttempts)
    {
        var now = timeProvider.GetUtcNow();
        return new Notification
        {
            Id = Guid.CreateVersion7(),
            Channel = channel,
            Recipient = recipient,
            TemplateKey = templateKey,
            Locale = locale,
            Payload = payload,
            Subject = content.Subject,
            Body = content.Body,
            IsHtml = content.IsHtml,
            Status = NotificationStatus.Pending,
            AttemptCount = 0,
            MaxAttempts = maxAttempts,
            NextAttemptAt = now,
            CorrelationId = correlationId,
            CreatedAt = now
        };
    }

    public bool IsDue(DateTimeOffset now) =>
        Status is NotificationStatus.Pending or NotificationStatus.Failed && NextAttemptAt <= now;

    /// <summary>Marks the notification as being handed to the provider.</summary>
    public void MarkSending()
    {
        EnsureNotTerminal();
        Status = NotificationStatus.Sending;
    }

    /// <summary>Marks a successful delivery. Terminal.</summary>
    public void MarkSent(string provider, string? providerMessageId)
    {
        Status = NotificationStatus.Sent;
        Provider = provider;
        ProviderMessageId = providerMessageId;
        LastError = null;
    }

    /// <summary>
    /// Records a failed attempt. Reschedules with exponential backoff, or moves to
    /// <see cref="NotificationStatus.Dead"/> once <see cref="MaxAttempts"/> is reached.
    /// </summary>
    public void MarkFailed(string error, TimeProvider timeProvider)
    {
        AttemptCount++;
        LastError = Truncate(error);

        if (AttemptCount >= MaxAttempts)
        {
            Status = NotificationStatus.Dead;
            return;
        }

        Status = NotificationStatus.Failed;
        NextAttemptAt = timeProvider.GetUtcNow() + BackoffFor(AttemptCount);
    }

    /// <summary>Forces the notification into the dead-letter state. Terminal.</summary>
    public void MarkDead(string error)
    {
        Status = NotificationStatus.Dead;
        LastError = Truncate(error);
    }

    private void EnsureNotTerminal()
    {
        if (Status is NotificationStatus.Sent or NotificationStatus.Dead)
            throw new InvalidOperationException($"Notification {Id} is in terminal state {Status}.");
    }

    // 1, 2, 4, 8, ... minutes, capped at one hour.
    private static TimeSpan BackoffFor(int attempt) =>
        TimeSpan.FromMinutes(Math.Min(60d, Math.Pow(2, attempt - 1)));

    private static string Truncate(string value) =>
        value.Length <= MaxErrorLength ? value : value[..MaxErrorLength];
}
