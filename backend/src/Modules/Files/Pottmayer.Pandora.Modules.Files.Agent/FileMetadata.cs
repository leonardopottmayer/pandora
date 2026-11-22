namespace Pottmayer.Pandora.Modules.Files.Agent;

/// <summary>
/// What a file's bytes say about it (product-plan F2): EXIF of photos, duration and resolution of video and
/// audio, tags of music, title and pages of PDFs. Every field is optional; an empty record means "read,
/// nothing found", so the file is not asked about again until its content changes.
/// </summary>
/// <param name="TakenAt">When the photo was taken, by the camera's clock (no time zone).</param>
/// <param name="Camera">Make and model.</param>
/// <param name="DurationSeconds">Video and audio.</param>
/// <param name="Width">Photos and video, as stored (a portrait photo may be rotated on display).</param>
public sealed record FileMetadata(
    DateTime? TakenAt = null,
    string? Camera = null,
    double? Latitude = null,
    double? Longitude = null,
    int? Width = null,
    int? Height = null,
    int? DurationSeconds = null,
    string? Title = null,
    string? Artist = null,
    string? Album = null,
    int? Pages = null)
{
    public const int MaxText = 500;

    private static readonly HashSet<string> Readable =
    [
        // Photos (EXIF)
        "jpg", "jpeg", "png", "gif", "bmp", "tif", "tiff", "webp", "heic", "heif",
        "cr2", "cr3", "nef", "arw", "dng", "orf", "rw2", "raf",
        // Video
        "mkv", "webm", "mp4", "m4v", "mov", "avi", "wmv", "mpg", "mpeg",
        // Audio
        "mp3", "flac", "m4a", "ogg", "opus", "wav", "wma", "aac", "aiff",
        // Documents
        "pdf",
    ];

    /// <summary>Whether an agent reads metadata from files with this extension; the backend asks only for these.</summary>
    public static bool IsReadable(string? extension) => extension is not null && Readable.Contains(extension);

    /// <summary>
    /// What the catalog stores: text trimmed, capped and without control characters (tags often carry NUL
    /// padding, which PostgreSQL's <c>jsonb</c> rejects), and values that cannot be right dropped.
    /// </summary>
    public FileMetadata Normalize()
    {
        // 0,0 is what a GPS without a fix writes.
        var located = Latitude is >= -90 and <= 90 && Longitude is >= -180 and <= 180 && !(Latitude == 0 && Longitude == 0);
        return new(
            TakenAt is { Year: >= 1900 and <= 2100 } ? TakenAt : null,
            Text(Camera),
            located ? Latitude : null,
            located ? Longitude : null,
            Positive(Width), Positive(Height), Positive(DurationSeconds),
            Text(Title), Text(Artist), Text(Album),
            Positive(Pages));
    }

    private static int? Positive(int? value) => value > 0 ? value : null;

    private static string? Text(string? value)
    {
        if (value is null) return null;
        var clean = new string([.. value.Where(c => !char.IsControl(c))]).Trim();
        return clean.Length == 0 ? null : clean.Length > MaxText ? clean[..MaxText] : clean;
    }
}
