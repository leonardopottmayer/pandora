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
    DateTimeOffset CreatedAt)
{
    public const string BasePath = "/api/v1/notes/media";

    public static MediaItemDto From(MediaItem m, bool hasThumbnail) =>
        new(m.Id, m.FolderId, m.Kind, m.Name, m.OriginalFileName, m.ContentType, m.SizeBytes, m.Width, m.Height,
            $"{BasePath}/{m.Id}", hasThumbnail ? $"{BasePath}/{m.Id}/thumbnail" : null, m.CreatedAt);
}

/// <summary>A folder with how many items it holds directly.</summary>
public sealed record MediaFolderDto(Guid Id, Guid? ParentId, string Name, int ItemCount)
{
    public static MediaFolderDto From(MediaFolder f, int itemCount) => new(f.Id, f.ParentId, f.Name, itemCount);
}

/// <summary>The library's folder tree as a flat list, plus the counts of the two places that are not folders.</summary>
public sealed record MediaFoldersDto(IReadOnlyList<MediaFolderDto> Folders, int UnsortedCount, int TotalCount);

/// <summary>The bytes and headers needed to serve an item, or one of its renditions.</summary>
public sealed record MediaContentDto(string FileName, string ContentType, byte[] Content);
