namespace Pottmayer.Pandora.Shared.Domain.Storage;

/// <summary>A blob read back from an <see cref="IFileStorage"/> backend, with the metadata needed to serve it.</summary>
public sealed record StoredFile(string FileName, string ContentType, long SizeBytes, byte[] Content);

/// <summary>Names of the storage backends an <see cref="IFileStorage.Backend"/> can report.</summary>
public static class FileStorageBackends
{
    /// <summary>Bytes kept in a Postgres table — the only backend in the MVP.</summary>
    public const string Database = "Database";
}

/// <summary>
/// Abstraction over binary blob storage. The MVP ships a single Postgres-backed implementation; the
/// interface exists so an S3/MinIO backend can replace it later without touching callers. A save
/// returns an opaque <c>storageKey</c> the caller persists (e.g. on an attachment record) and later
/// passes back to read or delete the blob. Saving is deliberately decoupled from any surrounding
/// transaction — mirroring object storage, where the write and its metadata row commit separately.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Identifies the backend that produced a key (see <see cref="FileStorageBackends"/>). Recorded
    /// on the attachment so a future migration can tell database blobs from S3 objects by reading the
    /// row rather than guessing.
    /// </summary>
    string Backend { get; }

    /// <summary>Stores the bytes and returns the key that locates them within <see cref="Backend"/>.</summary>
    Task<string> SaveAsync(string fileName, string contentType, byte[] content, CancellationToken ct = default);

    /// <summary>Reads a blob by its key, or <c>null</c> if no blob has that key.</summary>
    Task<StoredFile?> GetAsync(string storageKey, CancellationToken ct = default);

    /// <summary>Removes a blob by its key. No-op if it is already gone.</summary>
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}
