using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>
/// A version derived from a <see cref="MediaItem"/>, with bytes of its own (nte009): the thumbnail of
/// an image today, a video's poster frame or smaller encoding one day (see
/// <see cref="ValueObjects.RenditionPurpose"/>). Written once at upload, never edited.
/// </summary>
public sealed class MediaRendition : AggregateRoot<Guid>
{
    public Guid MediaId { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string StorageBackend { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private MediaRendition() { }

    public static MediaRendition Create(
        Guid mediaId,
        string purpose,
        int width,
        int height,
        string contentType,
        long sizeBytes,
        string storageBackend,
        string storageKey,
        TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            MediaId = mediaId,
            Purpose = purpose,
            Width = width,
            Height = height,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StorageBackend = storageBackend,
            StorageKey = storageKey,
            CreatedAt = timeProvider.GetUtcNow()
        };
}
