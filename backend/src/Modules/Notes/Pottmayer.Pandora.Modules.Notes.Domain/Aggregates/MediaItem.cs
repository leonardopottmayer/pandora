using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>
/// An item of the media library (nte002): an image or a file uploaded to Notes, a video one day (see
/// <see cref="ValueObjects.MediaKind"/>). The bytes live in an <c>IFileStorage</c> backend; this row holds
/// the metadata plus the (<see cref="StorageBackend"/>, <see cref="StorageKey"/>) pair that locates
/// them, so a bucket backend can come later without touching the rows already written.
///
/// The library is the user's, shared by every space. <see cref="FolderId"/> null is "Unsorted".
/// <see cref="Name"/> is what the library shows and can be renamed; <see cref="OriginalFileName"/>
/// stays the name of a download. Derived versions, like the thumbnail, are <see cref="MediaRendition"/>s.
/// </summary>
public sealed class MediaItem : AggregateRoot<Guid>, IAuditable
{
    /// <summary>The uploader; only they can see or download it.</summary>
    public Guid UserId { get; private set; }
    public Guid? FolderId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public int? Width { get; private set; }
    public int? Height { get; private set; }
    public string StorageBackend { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private MediaItem() { }

    /// <summary>Records an upload whose bytes are already stored under <paramref name="storageKey"/>.</summary>
    public static MediaItem Create(
        Guid userId,
        Guid? folderId,
        string kind,
        string fileName,
        string contentType,
        long sizeBytes,
        int? width,
        int? height,
        string storageBackend,
        string storageKey,
        TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            FolderId = folderId,
            Kind = kind,
            Name = fileName,
            OriginalFileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Width = width,
            Height = height,
            StorageBackend = storageBackend,
            StorageKey = storageKey,
            CreatedAt = timeProvider.GetUtcNow()
        };

    public void Rename(string name) => Name = name.Trim();

    public void MoveTo(Guid? folderId) => FolderId = folderId;

    /// <summary>
    /// Takes the item out of the library. The bytes stay: a note may still embed it, and which ones do
    /// is only known once notes track their media.
    /// </summary>
    public void Delete(TimeProvider timeProvider) => DeletedAt ??= timeProvider.GetUtcNow();
}
