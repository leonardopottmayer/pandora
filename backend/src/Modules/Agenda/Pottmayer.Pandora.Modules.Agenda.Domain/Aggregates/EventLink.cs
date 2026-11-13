using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

/// <summary>
/// One Pandora <see cref="Event"/> linked to one Google event of a <see cref="CalendarBinding"/> (doc
/// agd010): what keeps a sync from duplicating events. <see cref="SeriesRemoteId"/> is set on the
/// single events made from the instances of a Google series Pandora's recurrence engine cannot read.
/// </summary>
public sealed class EventLink : AggregateRoot<Guid>
{
    public Guid BindingId { get; private set; }
    public Guid EventId { get; private set; }
    public string RemoteEventId { get; private set; } = string.Empty;
    public string? SeriesRemoteId { get; private set; }

    /// <summary>Google's version of the event as last seen or sent. Null ⇒ born in Pandora and not in Google yet.</summary>
    public string? Etag { get; private set; }

    public DateTimeOffset? RemoteUpdatedAt { get; private set; }

    /// <summary>A hash of the Google reminders its alerts were written from; a different hash rewrites them.</summary>
    public string? RemindersHash { get; private set; }

    /// <summary>When Pandora last changed the event without Google having it yet. Null ⇒ nothing to send.</summary>
    public DateTimeOffset? PendingPushAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private EventLink() { }

    public bool IsInGoogle => Etag is not null;
    public bool IsPending => PendingPushAt is not null;

    public static EventLink Create(
        Guid bindingId, Guid eventId, string remoteEventId, string? seriesRemoteId, TimeProvider timeProvider) => new()
        {
            Id = Guid.CreateVersion7(),
            BindingId = bindingId,
            EventId = eventId,
            RemoteEventId = remoteEventId,
            SeriesRemoteId = seriesRemoteId,
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>
    /// An event created in Pandora, waiting to be inserted in Google. Pandora picks the Google id (lowercase
    /// hex is valid base32hex), so an insert retried after a lost answer finds its own event instead of
    /// making a second one.
    /// </summary>
    public static EventLink CreateLocal(Guid bindingId, Guid eventId, TimeProvider timeProvider)
    {
        var link = Create(bindingId, eventId, Guid.NewGuid().ToString("N"), null, timeProvider);
        link.PendingPushAt = link.CreatedAt;
        return link;
    }

    public void Touch(string? etag, DateTimeOffset? remoteUpdatedAt)
    {
        Etag = etag;
        RemoteUpdatedAt = remoteUpdatedAt;
    }

    public void SetRemindersHash(string hash) => RemindersHash = hash;

    /// <summary>Pandora changed the event: send it on the next push.</summary>
    public void MarkPending(DateTimeOffset now) => PendingPushAt = now;

    /// <summary>Google has Pandora's version, or Google's newer edit won.</summary>
    public void ClearPending() => PendingPushAt = null;
}
