namespace Pottmayer.Pandora.Modules.Notes.Domain.Ports;

/// <summary>The size of an image, as the image itself says it, once its orientation is applied.</summary>
public sealed record ImageSize(int Width, int Height);

/// <summary>A scaled-down copy of an image, encoded and ready to store.</summary>
public sealed record EncodedImage(byte[] Content, string ContentType, int Width, int Height);

/// <summary>
/// Reads and resizes uploaded images. Both calls answer <c>null</c> for bytes that are not an image
/// the processor can decode, so a format it does not know is kept as a plain file instead of failing.
/// </summary>
public interface IImageProcessor
{
    ImageSize? Inspect(byte[] content);

    /// <summary>The image fit within <paramref name="maxSide"/> on its longer side, rotated upright.</summary>
    EncodedImage? Thumbnail(byte[] content, int maxSide);
}
