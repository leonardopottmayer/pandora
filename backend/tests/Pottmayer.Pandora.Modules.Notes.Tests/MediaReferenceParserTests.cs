using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class MediaReferenceParserTests
{
    private static readonly Guid Beach = Guid.Parse("0193f000-0000-7000-8000-00000000a001");
    private static readonly Guid Receipt = Guid.Parse("0193f000-0000-7000-8000-00000000a002");

    [Fact]
    public void Finds_images_and_file_links_once_each_in_order()
    {
        var markdown = $"""
            ![Praia|400](/api/v1/notes/media/{Beach})
            See [receipt.pdf](/api/v1/notes/media/{Receipt}) and the beach again ![](/api/v1/notes/media/{Beach}/thumbnail).
            """;

        Assert.Equal([Beach, Receipt], MediaReferenceParser.Parse(markdown));
    }

    [Fact]
    public void Skips_paths_written_as_code()
    {
        var markdown = $"""
            Inline `![x](/api/v1/notes/media/{Beach})` is an example.

            ```
            ![x](/api/v1/notes/media/{Receipt})
            ```
            """;

        Assert.Empty(MediaReferenceParser.Parse(markdown));
    }

    [Fact]
    public void Ignores_other_paths()
    {
        Assert.Empty(MediaReferenceParser.Parse("![x](/api/v1/notes/media/not-a-guid) [y](https://example.com)"));
    }
}
