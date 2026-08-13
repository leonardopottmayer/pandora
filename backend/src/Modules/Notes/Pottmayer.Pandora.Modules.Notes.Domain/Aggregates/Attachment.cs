using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

/// <summary>
/// A binary file uploaded to the Notes module — an embedded image, a PDF, a zip. The bytes live in an
/// <c>IFileStorage</c> backend; this record holds only the metadata plus the <see cref="StorageKey"/>
/// that locates them. It is write-once (no edits after upload), so it carries just a
/// <see cref="CreatedAt"/> and no audit trail. <see cref="PageId"/> is optional: an attachment may be
/// created before it is embedded into any page.
/// </summary>
public sealed class Attachment : AggregateRoot<Guid>
{
    public Guid? PageId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }

    /// <summary>Which <c>IFileStorage</c> backend holds the bytes (e.g. <c>Database</c>, later <c>S3</c>).</summary>
    public string StorageBackend { get; private set; } = string.Empty;

    /// <summary>The opaque key that locates the bytes within <see cref="StorageBackend"/>.</summary>
    public string StorageKey { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    private Attachment() { }

    /// <summary>
    /// Records an uploaded blob whose bytes are already stored under <paramref name="storageKey"/> in
    /// <paramref name="storageBackend"/>.
    /// </summary>
    public static Attachment Create(
        Guid? pageId,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageBackend,
        string storageKey,
        TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            PageId = pageId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StorageBackend = storageBackend,
            StorageKey = storageKey,
            CreatedAt = timeProvider.GetUtcNow()
        };
}
