using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.Storage;

/// <summary>
/// A blob of bytes held in Postgres — the storage detail behind <see cref="DatabaseFileStorage"/>. It
/// is intentionally not a domain concept: callers see only the opaque key (this row's id). The
/// filename/content-type are duplicated onto the <c>Attachment</c> that references it, so a future S3
/// backend needs no schema change.
/// </summary>
public sealed class FileBlob : AggregateRoot<Guid>
{
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public byte[] Content { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }

    private FileBlob() { }

    public static FileBlob Create(string fileName, string contentType, byte[] content, DateTimeOffset createdAt) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = content.Length,
            Content = content,
            CreatedAt = createdAt
        };
}
