using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;

/// <summary>A coarse kind of file, derived from its extension, for filtering a search.</summary>
public sealed record FileCategory : IDomainValue<FileCategory>
{
    public string Value { get; }

    private FileCategory(string value) => Value = value;

    public static readonly FileCategory Video    = new("video");
    public static readonly FileCategory Audio    = new("audio");
    public static readonly FileCategory Image    = new("image");
    public static readonly FileCategory Document = new("document");
    public static readonly FileCategory Ebook    = new("ebook");
    public static readonly FileCategory Archive  = new("archive");
    public static readonly FileCategory Code     = new("code");
    public static readonly FileCategory Other    = new("other");

    public static readonly IReadOnlyList<FileCategory> All = [Video, Audio, Image, Document, Ebook, Archive, Code, Other];

    private static readonly Dictionary<string, FileCategory> ByExtension = Map(
        (Video, "mkv mp4 avi mov wmv flv webm m4v mpg mpeg ts m2ts vob 3gp"),
        (Audio, "mp3 flac wav aac ogg m4a wma opus aiff alac"),
        (Image, "jpg jpeg png gif bmp tif tiff webp heic heif raw cr2 cr3 nef arw dng svg psd"),
        (Document, "pdf doc docx odt rtf txt md xls xlsx ods csv ppt pptx odp"),
        (Ebook, "epub mobi azw azw3 djvu cbz cbr fb2"),
        (Archive, "zip rar 7z tar gz bz2 xz tgz iso"),
        (Code, "cs js tsx jsx py java c cpp h go rs rb php html css scss json xml yml yaml sql sh ps1 kt swift"));

    /// <summary>The category of a lower-case extension (no dot); <see cref="Other"/> when unknown or absent.</summary>
    public static FileCategory FromExtension(string? extension) =>
        extension is not null && ByExtension.TryGetValue(extension, out var category) ? category : Other;

    public static bool IsSupported(string? value) => All.Any(x => x.Value == value);

    public static FileCategory FromValue(string value) =>
        All.FirstOrDefault(x => x.Value == value)
        ?? throw new ArgumentException($"Unsupported file category value: '{value}'.", nameof(value));

    public override string ToString() => Value;

    private static Dictionary<string, FileCategory> Map(params (FileCategory Category, string Extensions)[] groups) =>
        groups.SelectMany(g => g.Extensions.Split(' ').Select(e => (e, g.Category)))
              .ToDictionary(x => x.e, x => x.Category);
}
