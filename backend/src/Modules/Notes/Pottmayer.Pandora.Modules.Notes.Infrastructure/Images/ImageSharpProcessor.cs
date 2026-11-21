using Pottmayer.Pandora.Modules.Notes.Domain.Ports;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Pottmayer.Pandora.Modules.Notes.Infrastructure.Images;

/// <summary>
/// <see cref="IImageProcessor"/> over ImageSharp 3.1 — pure managed code, so the Docker image needs no
/// native library. Pinned to 3.1 on purpose: 3.2 and 4.x check for a license key at build time and fail a
/// Release build without one, while 3.1 is the same Six Labors Split License (Apache 2.0 below 1M USD
/// of revenue) with no check.
///
/// 3.1 has known decoder bugs fixed only in 3.2 (mostly TIFF), so only the formats people upload as
/// pictures are decoded; anything else is "not an image".
///
/// Phone photos often keep their rotation in EXIF instead of in the pixels; both calls apply it, so a
/// portrait photo reports and thumbnails as portrait.
/// </summary>
internal sealed class ImageSharpProcessor : IImageProcessor
{
    // WebP keeps transparency and is a fraction of a PNG or JPEG of the same thumbnail.
    private static readonly WebpEncoder ThumbnailEncoder = new() { Quality = 80 };

    private static readonly DecoderOptions Decoding = new()
    {
        Configuration = new Configuration(
            new PngConfigurationModule(),
            new JpegConfigurationModule(),
            new GifConfigurationModule(),
            new WebpConfigurationModule()),
    };

    public ImageSize? Inspect(byte[] content)
    {
        try
        {
            var info = Image.Identify(Decoding, content);
            // Orientations 5 to 8 turn the picture a quarter, which swaps its sides.
            var turned = info.Metadata.ExifProfile?.TryGetValue(
                SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Orientation, out var orientation) == true
                && orientation.Value is >= 5 and <= 8;
            return turned ? new ImageSize(info.Height, info.Width) : new ImageSize(info.Width, info.Height);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            return null;
        }
    }

    public EncodedImage? Thumbnail(byte[] content, int maxSide)
    {
        try
        {
            using var image = Image.Load(Decoding, content);
            image.Mutate(x => x
                .AutoOrient()
                .Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(maxSide, maxSide) }));

            using var output = new MemoryStream();
            image.Save(output, ThumbnailEncoder);
            return new EncodedImage(output.ToArray(), "image/webp", image.Width, image.Height);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            return null;
        }
    }
}
