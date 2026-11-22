using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Notes.Application.Dtos;

/// <summary>
/// An item of the media library. <see cref="Url"/> is the authenticated path a note embeds
/// (<c>![](/api/v1/notes/media/{id})</c>); <see cref="ThumbnailUrl"/> is null when the item has no
/// thumbnail — a file, or an image already small enough to show as it is.
/// </summary>
public sealed record MediaItemDto(
    Guid Id,
    Guid? FolderId,
    string Kind,
    string Name,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    int? Width,
    int? Height,
    string Url,
    string? ThumbnailUrl,
    int UsageCount,
    bool IsDeleted,
    DateTimeOffset CreatedAt)
{
    public const string BasePath = "/api/v1/notes/media";

    /// <param name="usageCount">How many pages show the item.</param>
    public static MediaItemDto From(MediaItem m, bool hasThumbnail, int usageCount = 0) =>
        new(m.Id, m.FolderId, m.Kind, m.Name, m.OriginalFileName, m.ContentType, m.SizeBytes, m.Width, m.Height,
            $"{BasePath}/{m.Id}", hasThumbnail ? $"{BasePath}/{m.Id}/thumbnail" : null, usageCount,
            m.DeletedAt is not null, m.CreatedAt);
}

/// <summary>A page that shows a library item.</summary>
public sealed record MediaUsageDto(Guid PageId, Guid SpaceId, string Title, string? Icon, bool IsArchived);

/// <summary>A folder with how many items it holds directly.</summary>
public sealed record MediaFolderDto(Guid Id, Guid? ParentId, string Name, int ItemCount)
{
    public static MediaFolderDto From(MediaFolder f, int itemCount) => new(f.Id, f.ParentId, f.Name, itemCount);
}

/// <summary>
/// The library's folder tree as a flat list, plus the counts of the places that are not folders and the
/// space the items take — all of them, and the ones no page shows. Thumbnails are not counted.
/// </summary>
public sealed record MediaFoldersDto(
    IReadOnlyList<MediaFolderDto> Folders, int UnsortedCount, int UnusedCount, int TotalCount, long TotalBytes, long UnusedBytes);

/// <summary>The bytes and headers needed to serve an item, or one of its renditions.</summary>
public sealed record MediaContentDto(string FileName, string ContentType, byte[] Content);
