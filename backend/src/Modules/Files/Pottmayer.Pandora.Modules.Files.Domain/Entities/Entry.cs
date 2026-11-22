using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Files.Domain.Entities;

/// <summary>
/// One file or folder under a root, by its path relative to the root. A scan never deletes it: an entry
/// it does not see becomes missing or excluded and waits for the user in the review inbox. Its id
/// survives a move, recognized by the <see cref="Fingerprint"/>.
/// </summary>
public sealed class Entry
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RootId { get; private set; }
    public EntryKind Kind { get; private set; } = null!;

    /// <summary>Catalog form: <c>/Movies/a.mkv</c> (see <see cref="CatalogPath"/>).</summary>
    public string RelativePath { get; private set; } = string.Empty;
    public string ParentPath { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Extension { get; private set; }

    /// <summary>Null for folders.</summary>
    public FileCategory? Category { get; private set; }

    public long SizeBytes { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }

    /// <summary>Null for folders, and for a file until the agent computes it.</summary>
    public string? Fingerprint { get; private set; }

    /// <summary>
    /// What the bytes say (F2). Null for folders, for files the agent does not read, and for a readable
    /// file until the agent reads it; cleared when the content changes.
    /// </summary>
    public FileMetadata? Metadata { get; private set; }

    public EntryStatus Status { get; private set; } = EntryStatus.Present;
    public DateTimeOffset? MissingSince { get; private set; }

    /// <summary>The user chose to keep it although missing or excluded: it left the review inbox.</summary>
    public DateTimeOffset? KeptAt { get; private set; }

    public DateTimeOffset FirstSeenAt { get; private set; }
    public Guid? LastSeenScanId { get; private set; }

    private Entry() { }

    /// <summary>A path the catalog has not seen. Callers pass a valid, normalized path.</summary>
    public static Entry Create(
        Guid userId, Guid rootId, Guid scanId, EntryKind kind, string path,
        long size, DateTimeOffset? modifiedAt, string? fingerprint, FileMetadata? metadata, DateTimeOffset now)
    {
        var entry = new Entry
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            RootId = rootId,
            Kind = kind,
            FirstSeenAt = now,
            LastSeenScanId = scanId
        };
        entry.Place(rootId, path);
        entry.SetContent(size, modifiedAt, fingerprint, metadata);
        return entry;
    }

    public bool IsFile => Kind == EntryKind.File;

    public bool NeedsFingerprint => IsFile && Fingerprint is null;

    public bool NeedsMetadata => IsFile && Metadata is null && FileMetadata.IsReadable(Extension);

    /// <summary>
    /// Seen by a scan. A file whose size or date changed takes the new ones and loses its fingerprint and
    /// metadata unless they come with them, so the agent is asked again. Returns whether the content changed.
    /// </summary>
    public bool See(Guid scanId, long size, DateTimeOffset? modifiedAt, string? fingerprint, FileMetadata? metadata = null)
    {
        LastSeenScanId = scanId;
        Status = EntryStatus.Present;
        MissingSince = null;
        KeptAt = null;

        if (!IsFile) return false;

        var changed = SizeBytes != size || ModifiedAt != Truncate(modifiedAt);
        if (changed)
        {
            SetContent(size, modifiedAt, fingerprint, metadata);
            return true;
        }

        if (fingerprint is not null) Fingerprint = fingerprint;
        if (metadata is not null) Metadata = metadata.Normalize();
        return false;
    }

    /// <summary>
    /// The same file showed up at another path as <paramref name="newer"/>: this entry takes its place and
    /// facts, keeping its own id and history. The caller removes <paramref name="newer"/>.
    /// </summary>
    public void MoveTo(Entry newer)
    {
        Place(newer.RootId, newer.RelativePath);
        SizeBytes = newer.SizeBytes;
        ModifiedAt = newer.ModifiedAt;
        Fingerprint = newer.Fingerprint;
        Metadata = newer.Metadata ?? Metadata;
        LastSeenScanId = newer.LastSeenScanId;
        Status = EntryStatus.Present;
        MissingSince = null;
        KeptAt = null;
    }

    private void Place(Guid rootId, string path)
    {
        RootId = rootId;
        RelativePath = path;
        ParentPath = CatalogPath.Parent(path);
        Name = CatalogPath.Name(path);
        Extension = IsFile ? CatalogPath.Extension(Name) : null;
        Category = IsFile ? FileCategory.FromExtension(Extension) : null;
    }

    private void SetContent(long size, DateTimeOffset? modifiedAt, string? fingerprint, FileMetadata? metadata)
    {
        SizeBytes = IsFile ? size : 0;
        ModifiedAt = IsFile ? Truncate(modifiedAt) : null;
        Fingerprint = IsFile ? fingerprint : null;
        Metadata = IsFile ? metadata?.Normalize() : null;
    }

    /// <summary>
    /// To the microsecond and in UTC, as PostgreSQL stores it, so a date read back compares equal to the
    /// one the agent sends again — otherwise every file would look changed on every scan.
    /// </summary>
    private static DateTimeOffset? Truncate(DateTimeOffset? value) =>
        value is { } v ? new DateTimeOffset(v.UtcTicks - v.UtcTicks % 10, TimeSpan.Zero) : null;
}
