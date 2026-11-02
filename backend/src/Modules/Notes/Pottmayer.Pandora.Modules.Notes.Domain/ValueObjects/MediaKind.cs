namespace Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

/// <summary>
/// What a media item is, which decides how it is shown: an image draws inline, a file is a download.
/// <see cref="Video"/> is part of the schema already, but not accepted yet: storing and streaming
/// videos needs a bucket and ranged downloads, which the database blob store does not give.
/// </summary>
public static class MediaKind
{
    public const string Image = "image";
    public const string File = "file";
    public const string Video = "video";

    /// <summary>The kinds an upload is accepted as today. Adding video starts here.</summary>
    public static readonly IReadOnlySet<string> Accepted = new HashSet<string> { Image, File };

    /// <summary>
    /// The kind a content type claims. An image the server cannot read still lands as a file — the
    /// upload decides that, once it has tried.
    /// </summary>
    public static string FromContentType(string contentType) =>
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? Image
        : contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? Video
        : File;
}

/// <summary>Why a rendition of a media item exists.</summary>
public static class RenditionPurpose
{
    /// <summary>A small image for the library grid: the image scaled down, or a video's frame one day.</summary>
    public const string Thumbnail = "thumbnail";

    /// <summary>The longest side of a thumbnail, in pixels. An image already this small gets none.</summary>
    public const int ThumbnailMaxSide = 400;
}
