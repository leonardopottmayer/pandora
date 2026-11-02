using System.Text;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Notes.Infrastructure.Images;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class ImageSharpProcessorTests
{
    private readonly ImageSharpProcessor _processor = new();

    /// <summary>A landscape picture whose EXIF says "turn me a quarter", the way phones save portraits.</summary>
    private static byte[] TurnedJpeg(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(200, 80, 40));
        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)6);
        using var output = new MemoryStream();
        image.Save(output, new JpegEncoder());
        return output.ToArray();
    }

    [Fact]
    public void Reports_a_phone_portrait_as_portrait()
    {
        Assert.Equal(new(300, 800), _processor.Inspect(TurnedJpeg(800, 300)));
    }

    [Fact]
    public void Thumbnails_upright_within_the_longest_side_as_webp()
    {
        var thumbnail = _processor.Thumbnail(TurnedJpeg(800, 300), RenditionPurpose.ThumbnailMaxSide)!;

        Assert.Equal("image/webp", thumbnail.ContentType);
        Assert.Equal((150, 400), (thumbnail.Width, thumbnail.Height));
        using var decoded = Image.Load(thumbnail.Content);
        Assert.Equal((150, 400), (decoded.Width, decoded.Height));
    }

    [Fact]
    public void Answers_null_for_bytes_that_are_not_an_image()
    {
        var bytes = Encoding.UTF8.GetBytes("not an image");
        Assert.Null(_processor.Inspect(bytes));
        Assert.Null(_processor.Thumbnail(bytes, 400));
    }

    [Theory]
    [InlineData("image/png", "image")]
    [InlineData("IMAGE/JPEG", "image")]
    [InlineData("video/mp4", "video")]
    [InlineData("application/pdf", "file")]
    public void Kind_follows_the_content_type(string contentType, string kind)
    {
        Assert.Equal(kind, MediaKind.FromContentType(contentType));
    }

    [Fact]
    public void Video_is_known_but_not_accepted_yet()
    {
        Assert.DoesNotContain(MediaKind.Video, MediaKind.Accepted);
    }
}
