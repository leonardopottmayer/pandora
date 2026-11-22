using MetadataExtractor;
using MetadataExtractor.Formats.Bmp;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Gif;
using MetadataExtractor.Formats.Heif;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Png;
using MetadataExtractor.Formats.WebP;
using Pottmayer.Pandora.Modules.Files.Agent;
using UglyToad.PdfPig;

namespace Pottmayer.Pandora.Desktop.Files;

/// <summary>
/// Reads what a file's bytes say (product-plan F2): EXIF with MetadataExtractor, video and audio with
/// TagLib#, PDFs with PdfPig. Headers only, read-only, sharing everything like the fingerprint.
/// </summary>
internal static class MetadataReader
{
    private static readonly HashSet<string> Media =
        ["mkv", "webm", "mp4", "m4v", "mov", "avi", "wmv", "mpg", "mpeg", "mp3", "flac", "m4a", "ogg", "opus", "wav", "wma", "aac", "aiff"];

    /// <summary>Where each image format keeps its size, the most trustworthy first.</summary>
    private static readonly (Type Directory, int Width, int Height)[] Sizes =
    [
        (typeof(JpegDirectory), JpegDirectory.TagImageWidth, JpegDirectory.TagImageHeight),
        (typeof(PngDirectory), PngDirectory.TagImageWidth, PngDirectory.TagImageHeight),
        (typeof(GifHeaderDirectory), GifHeaderDirectory.TagImageWidth, GifHeaderDirectory.TagImageHeight),
        (typeof(BmpHeaderDirectory), BmpHeaderDirectory.TagImageWidth, BmpHeaderDirectory.TagImageHeight),
        (typeof(WebPDirectory), WebPDirectory.TagImageWidth, WebPDirectory.TagImageHeight),
        (typeof(HeicImagePropertiesDirectory), HeicImagePropertiesDirectory.TagImageWidth, HeicImagePropertiesDirectory.TagImageHeight),
        (typeof(ExifSubIfdDirectory), ExifDirectoryBase.TagExifImageWidth, ExifDirectoryBase.TagExifImageHeight),
        (typeof(ExifIfd0Directory), ExifDirectoryBase.TagImageWidth, ExifDirectoryBase.TagImageHeight),
    ];

    /// <summary>
    /// Null when the file cannot be read now (locked, gone), so it is asked about again on the next scan;
    /// an empty record when it was read but says nothing a reader understands (damaged, encrypted, unusual).
    /// </summary>
    public static FileMetadata? Read(string fullPath)
    {
        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var extension = CatalogPath.Extension(Path.GetFileName(fullPath));
            try
            {
                return extension == "pdf" ? ReadPdf(stream)
                    : Media.Contains(extension ?? "") ? ReadMedia(stream, fullPath)
                    : ReadPhoto(stream);
            }
            catch (Exception ex) when (ex is not (IOException or UnauthorizedAccessException))
            {
                return new FileMetadata();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static FileMetadata ReadPhoto(Stream stream)
    {
        var directories = ImageMetadataReader.ReadMetadata(stream);
        var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        var exif = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        var location = directories.OfType<GpsDirectory>().FirstOrDefault()?.GetGeoLocation();
        var (width, height) = SizeOf(directories);

        DateTime? takenAt = exif is not null && exif.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var taken)
            ? DateTime.SpecifyKind(taken, DateTimeKind.Unspecified)
            : null;

        // "Canon" + "Canon EOS R6" reads better as the model alone.
        var make = ifd0?.GetDescription(ExifDirectoryBase.TagMake)?.Trim();
        var model = ifd0?.GetDescription(ExifDirectoryBase.TagModel)?.Trim();
        var camera = string.IsNullOrEmpty(make) || model?.StartsWith(make, StringComparison.OrdinalIgnoreCase) == true
            ? model
            : $"{make} {model}".Trim();

        return new FileMetadata(takenAt, camera, location?.Latitude, location?.Longitude, width, height);
    }

    private static (int?, int?) SizeOf(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        foreach (var (type, widthTag, heightTag) in Sizes)
            foreach (var directory in directories.Where(type.IsInstanceOfType))
                if (directory.TryGetInt32(widthTag, out var width) && directory.TryGetInt32(heightTag, out var height))
                    return (width, height);
        return (null, null);
    }

    private static FileMetadata ReadMedia(Stream stream, string fullPath)
    {
        using var file = TagLib.File.Create(new ReadOnlyFile(fullPath, stream), TagLib.ReadStyle.Average);
        var properties = file.Properties;
        var tag = file.Tag;
        return new FileMetadata(
            Width: properties?.VideoWidth,
            Height: properties?.VideoHeight,
            DurationSeconds: properties is null ? null : (int)Math.Round(properties.Duration.TotalSeconds),
            Title: tag.Title,
            Artist: tag.FirstPerformer ?? tag.FirstAlbumArtist,
            Album: tag.Album);
    }

    private static FileMetadata ReadPdf(Stream stream)
    {
        using var document = PdfDocument.Open(stream);
        return new FileMetadata(Title: document.Information.Title, Pages: document.NumberOfPages);
    }

    /// <summary>Hands TagLib# the stream opened here; its name tells TagLib# the format.</summary>
    private sealed class ReadOnlyFile(string name, Stream stream) : TagLib.File.IFileAbstraction
    {
        public string Name => name;
        public Stream ReadStream => stream;
        public Stream WriteStream => throw new NotSupportedException("The agent never writes to the disk.");
        public void CloseStream(Stream stream) { } // closed by whoever opened it
    }
}
