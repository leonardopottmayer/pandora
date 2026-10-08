using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Files.Domain.Entities;

/// <summary>
/// A top-level location on a paired device — a whole disk (<c>E:\</c>, <c>/mnt/hd</c>) or a folder — that
/// the device's agent scans, with the selection marks saying which folders inside it are in.
/// </summary>
public sealed class Root : IAuditable
{
    public const int NameMaxLength = 100;
    public const int LocalPathMaxLength = 1024;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>The Identity device whose agent scans this root.</summary>
    public Guid DeviceId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>As the device gives it (<c>E:\</c>, <c>/Volumes/Fotos</c>); never rewritten.</summary>
    public string LocalPath { get; private set; } = string.Empty;

    /// <summary>Drives selection marks and the filters' default. Insensitive on Windows and macOS by default.</summary>
    public bool CaseSensitive { get; private set; }

    public bool IncludeHidden { get; private set; }

    /// <summary>Daily scan time, in the device's local time; null scans only on demand.</summary>
    public TimeOnly? ScanTime { get; private set; }

    public RootStatus Status { get; private set; } = RootStatus.Active;
    public DateTimeOffset? LastCompletedScanAt { get; private set; }
    public int EntryCount { get; private set; }

    private readonly List<SelectionMark> _marks = [];
    public IReadOnlyList<SelectionMark> Marks => _marks;

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private Root() { }

    /// <summary>Callers validate with <see cref="IsValidName"/> and <see cref="IsValidLocalPath"/> first.</summary>
    public static Root Create(
        Guid userId, Guid deviceId, string name, string localPath, bool caseSensitive, bool includeHidden, TimeOnly? scanTime)
        => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            DeviceId = deviceId,
            Name = name.Trim(),
            LocalPath = localPath.Trim(),
            CaseSensitive = caseSensitive,
            IncludeHidden = includeHidden,
            ScanTime = scanTime
        };

    public bool IsActive => Status == RootStatus.Active;

    public void Update(string name, TimeOnly? scanTime, bool includeHidden, bool caseSensitive)
    {
        Name = name.Trim();
        ScanTime = scanTime;
        IncludeHidden = includeHidden;
        CaseSensitive = caseSensitive;
    }

    /// <summary>Stops scanning it. Its entries are not deleted — they become excluded and go to the review inbox.</summary>
    public void Remove() => Status = RootStatus.Removed;

    /// <summary>Adding the same path again brings a removed root back, with its catalog.</summary>
    public void Restore(string name, TimeOnly? scanTime, bool includeHidden, bool caseSensitive)
    {
        Status = RootStatus.Active;
        Update(name, scanTime, includeHidden, caseSensitive);
    }

    /// <summary>Replaces the marks as a set. Callers pass normalized, distinct paths.</summary>
    public void ReplaceSelection(IEnumerable<(string Path, SelectionMode Mode)> marks)
    {
        _marks.Clear();
        _marks.AddRange(marks.Select(m => SelectionMark.Create(Id, m.Path, m.Mode)));
    }

    public void RecordCompletedScan(DateTimeOffset now, int entryCount)
    {
        LastCompletedScanAt = now;
        EntryCount = entryCount;
    }

    public static bool IsValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= NameMaxLength;

    public static bool IsValidLocalPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && path.Trim().Length <= LocalPathMaxLength;
}
