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
    public string? Etag { get; private set; }
    public DateTimeOffset? RemoteUpdatedAt { get; private set; }

    /// <summary>A hash of the Google reminders its alerts were written from; a different hash rewrites them.</summary>
    public string? RemindersHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private EventLink() { }

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

    public void Touch(string? etag, DateTimeOffset? remoteUpdatedAt)
    {
        Etag = etag;
        RemoteUpdatedAt = remoteUpdatedAt;
    }

    public void SetRemindersHash(string hash) => RemindersHash = hash;
}
