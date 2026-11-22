using System.Text;
using Pottmayer.Pandora.Modules.Files.Agent;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace Pottmayer.Pandora.Desktop.Files.Tests;

/// <summary>Each reader against a small file of its format, built here byte by byte.</summary>
public sealed class MetadataReaderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("pandora-metadata-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void A_photo_gives_when_where_with_what_and_its_size()
    {
        var metadata = Read("IMG_1.jpg", Jpeg(width: 600, height: 400));

        Assert.NotNull(metadata);
        Assert.Equal(new DateTime(2024, 7, 10, 14, 0, 0), metadata.TakenAt);
        Assert.Equal("Canon EOS R6", metadata.Camera);
        Assert.Equal(-27.6, metadata.Latitude!.Value, 6);
        Assert.Equal(-48.5, metadata.Longitude!.Value, 6);
        Assert.Equal((600, 400), (metadata.Width, metadata.Height));
    }

    [Fact]
    public void Audio_gives_its_duration()
    {
        Assert.Equal(1, Read("beep.wav", Wav(seconds: 1))?.DurationSeconds);
    }

    [Fact]
    public void A_pdf_gives_its_title_and_pages()
    {
        var builder = new PdfDocumentBuilder();
        builder.DocumentInformation.Title = "Fridge manual";
        builder.AddPage(PageSize.A4);
        builder.AddPage(PageSize.A4);

        var metadata = Read("manual.pdf", builder.Build());

        Assert.Equal(("Fridge manual", 2), (metadata?.Title, metadata?.Pages));
    }

    [Fact]
    public void A_damaged_file_says_nothing_but_a_missing_one_is_asked_again()
    {
        Assert.Equal(new FileMetadata(), Read("broken.jpg", Encoding.ASCII.GetBytes("not a photo")));
        Assert.Null(MetadataReader.Read(Path.Combine(_root, "gone.jpg")));
    }

    private FileMetadata? Read(string name, byte[] content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllBytes(path, content);
        return MetadataReader.Read(path);
    }

    // ── Files, built by hand ──

    /// <summary>A JPEG with no pixels: EXIF (camera, date, GPS at 27°36'S 48°30'W) and the frame header.</summary>
    private static byte[] Jpeg(int width, int height)
    {
        Tag[] gps =
        [
            Ascii(0x0001, "S"), Rationals(0x0002, (27, 1), (36, 1), (0, 1)),
            Ascii(0x0003, "W"), Rationals(0x0004, (48, 1), (30, 1), (0, 1)),
        ];
        Tag[] exif = [Ascii(0x9003, "2024:07:10 14:00:00")];
        Tag[] ifd0Without = [Ascii(0x010F, "Canon"), Ascii(0x0110, "Canon EOS R6"), Long(0x8769, 0), Long(0x8825, 0)];
        var exifOffset = 8 + SizeOf(ifd0Without);
        var gpsOffset = exifOffset + SizeOf(exif);
        Tag[] ifd0 = [ifd0Without[0], ifd0Without[1], Long(0x8769, (uint)exifOffset), Long(0x8825, (uint)gpsOffset)];

        var tiff = new MemoryStream();
        using (var w = new BinaryWriter(tiff, Encoding.ASCII, leaveOpen: true))
        {
            w.Write("II"u8); w.Write((ushort)42); w.Write(8u);
            WriteIfd(w, ifd0);
            WriteIfd(w, exif);
            WriteIfd(w, gps);
        }

        var jpeg = new MemoryStream();
        jpeg.Write([0xFF, 0xD8]);
        Segment(jpeg, 0xE1, [.. "Exif\0\0"u8, .. tiff.ToArray()]);
        Segment(jpeg, 0xC0, [8, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 3, 1, 0x11, 0, 2, 0x11, 1, 3, 0x11, 1]);
        jpeg.Write([0xFF, 0xD9]);
        return jpeg.ToArray();
    }

    private static void Segment(Stream jpeg, byte marker, byte[] body)
    {
        var length = body.Length + 2;
        jpeg.Write([0xFF, marker, (byte)(length >> 8), (byte)length]);
        jpeg.Write(body);
    }

    private sealed record Tag(ushort Id, ushort Type, uint Count, byte[] Value);

    private static Tag Ascii(ushort id, string text) => new(id, 2, (uint)text.Length + 1, Encoding.ASCII.GetBytes(text + "\0"));

    private static Tag Long(ushort id, uint value) => new(id, 4, 1, BitConverter.GetBytes(value));

    private static Tag Rationals(ushort id, params (uint Numerator, uint Denominator)[] values) =>
        new(id, 5, (uint)values.Length, [.. values.SelectMany(v => BitConverter.GetBytes(v.Numerator).Concat(BitConverter.GetBytes(v.Denominator)))]);

    /// <summary>An IFD followed by the values that do not fit in its entries.</summary>
    private static int SizeOf(Tag[] ifd) => 2 + 12 * ifd.Length + 4 + ifd.Where(t => t.Value.Length > 4).Sum(t => t.Value.Length + t.Value.Length % 2);

    private static void WriteIfd(BinaryWriter w, Tag[] ifd)
    {
        var dataAt = (int)w.BaseStream.Position + 2 + 12 * ifd.Length + 4;
        var data = new MemoryStream();
        w.Write((ushort)ifd.Length);
        foreach (var tag in ifd)
        {
            w.Write(tag.Id); w.Write(tag.Type); w.Write(tag.Count);
            if (tag.Value.Length <= 4)
            {
                w.Write(tag.Value);
                w.Write(new byte[4 - tag.Value.Length]);
                continue;
            }
            w.Write((uint)(dataAt + data.Length));
            data.Write(tag.Value);
            if (tag.Value.Length % 2 == 1) data.WriteByte(0);
        }
        w.Write(0u); // no next IFD
        w.Write(data.ToArray());
    }

    /// <summary>Mono 8-bit PCM at 8 kHz: one byte per sample.</summary>
    private static byte[] Wav(int seconds)
    {
        const int rate = 8000;
        var samples = rate * seconds;
        var wav = new MemoryStream();
        using var w = new BinaryWriter(wav);
        w.Write("RIFF"u8); w.Write(36 + samples); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate); w.Write((short)1); w.Write((short)8);
        w.Write("data"u8); w.Write(samples); w.Write(new byte[samples]);
        w.Flush();
        return wav.ToArray();
    }
}
