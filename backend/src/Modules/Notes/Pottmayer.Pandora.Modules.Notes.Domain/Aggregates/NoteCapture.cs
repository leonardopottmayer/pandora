using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>A durable capture receipt keyed by inbound event id, retained when the page is purged.</summary>
public sealed class NoteCapture : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public Guid PageId { get; private set; }
    public DateTimeOffset CapturedAt { get; private set; }

    private NoteCapture() { }

    public static NoteCapture Record(Guid messageId, Guid userId, Guid pageId, TimeProvider timeProvider)
        => new() { Id = messageId, UserId = userId, PageId = pageId, CapturedAt = timeProvider.GetUtcNow() };
}
