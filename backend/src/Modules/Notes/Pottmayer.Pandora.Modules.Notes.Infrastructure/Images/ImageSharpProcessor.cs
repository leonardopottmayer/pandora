using Pottmayer.Pandora.Modules.Notes.Domain.Ports;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Pottmayer.Pandora.Modules.Notes.Infrastructure.Images;

/// <summary>
/// <see cref="IImageProcessor"/> over ImageSharp 3.1 — pure managed code, so the Docker image needs no
/// native library. Pinned below 4.x on purpose: 4.x checks for a license key at build time and fails a
/// Release build without one, while 3.1 is the same Six Labors Split License (Apache 2.0 below 1M USD
/// of revenue) with no check.
///
/// Phone photos often keep their rotation in EXIF instead of in the pixels; both calls apply it, so a
/// portrait photo reports and thumbnails as portrait.
/// </summary>
internal sealed class ImageSharpProcessor : IImageProcessor
{
    // WebP keeps transparency and is a fraction of a PNG or JPEG of the same thumbnail.
    private static readonly WebpEncoder ThumbnailEncoder = new() { Quality = 80 };

    public ImageSize? Inspect(byte[] content)
    {
        try
        {
            var info = Image.Identify(content);
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
            using var image = Image.Load(content);
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
