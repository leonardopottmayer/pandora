using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Infrastructure.Links;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class LinkPreviewTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.20.0.5")]
    [InlineData("192.168.0.10")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("64:ff9b::a00:1")]
    public void Private_and_reserved_addresses_are_not_public(string address) =>
        Assert.False(PublicAddress.Is(IPAddress.Parse(address)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("151.101.1.69")]
    [InlineData("2606:4700:4700::1111")]
    public void Internet_addresses_are_public(string address) =>
        Assert.True(PublicAddress.Is(IPAddress.Parse(address)));

    [Theory]
    [InlineData("http://127.0.0.1:9/")]
    [InlineData("http://localhost:9/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    public async Task A_link_to_the_server_own_network_is_never_fetched(string url)
    {
        using var http = new HttpClient(HttpLinkPreviewer.CreateHandler());
        var previewer = new HttpLinkPreviewer(http, NullLogger<HttpLinkPreviewer>.Instance);

        Assert.Null(await previewer.PreviewAsync(new Uri(url), CancellationToken.None));
    }

    [Fact]
    public void Reads_open_graph_tags_title_icon_and_the_oembed_link()
    {
        const string html = """
            <html><head>
            <title>Fallback</title>
            <meta property="og:title" content="Transactional outbox &amp; friends">
            <meta content='Keep the database and the broker in step.' name='description'>
            <meta property="og:image" content="/img/cover.png">
            <meta property="og:site_name" content="microservices.io">
            <link rel="shortcut icon" href="/favicon.png">
            <link rel="alternate" type="application/json+oembed" href="https://example.com/oembed?u=1">
            </head><body></body></html>
            """;
        var meta = LinkMetadata.ParseHtml(html, new Uri("https://microservices.io/patterns/outbox.html"));

        Assert.Equal("Transactional outbox & friends", meta.Title);
        Assert.Equal("Keep the database and the broker in step.", meta.Description);
        Assert.Equal("microservices.io", meta.SiteName);
        Assert.Equal("https://microservices.io/img/cover.png", meta.ImageUrl);
        Assert.Equal("https://microservices.io/favicon.png", meta.IconUrl);
        Assert.Equal(new Uri("https://example.com/oembed?u=1"), meta.OEmbedUrl);
    }

    [Fact]
    public void Falls_back_to_the_title_tag_and_drops_script_images()
    {
        var meta = LinkMetadata.ParseHtml("<title> Só o\n título </title><meta property=\"og:image\" content=\"javascript:alert(1)\">", new Uri("https://a.example/"));
        Assert.Equal("Só o título", meta.Title);
        Assert.Null(meta.ImageUrl);
    }

    [Fact]
    public void Reads_a_rich_oembed_and_refuses_anything_else()
    {
        var parsed = LinkMetadata.ParseOEmbed("""{"type":"rich","html":"<blockquote>x</blockquote>","author_name":"Ana","provider_name":"Bluesky","width":550}""");
        Assert.NotNull(parsed);
        Assert.Equal(("rich", "<blockquote>x</blockquote>", 550), (parsed.Value.Embed.Type, parsed.Value.Embed.Html, parsed.Value.Embed.Width));
        Assert.Equal(("Ana", "Bluesky"), (parsed.Value.Title, parsed.Value.SiteName));

        Assert.Null(LinkMetadata.ParseOEmbed("""{"type":"script"}"""));
        Assert.Null(LinkMetadata.ParseOEmbed("not json"));
    }
}
