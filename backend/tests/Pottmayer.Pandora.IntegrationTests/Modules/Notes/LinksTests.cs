using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

/// <summary>Covers the link preview endpoint with the web replaced by a fake: what it answers, and what it refuses.</summary>
[Collection("Integration")]
public sealed class LinksTests : IAsyncLifetime
{
    private const string PreviewUrl = "/api/v1/notes/links/preview?url=";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LinksTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<ILinkPreviewer>(new FakePreviewer())));
        _client = host.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Answers_what_the_page_says_about_itself()
    {
        await AuthAsync("links1");
        var response = await _client.GetAsync(PreviewUrl + Uri.EscapeDataString("https://example.com/post"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = (await response.Content.ReadFromJsonAsync<Envelope<PreviewItem>>())!.Data;
        Assert.Equal(("Um post", "rich"), (preview.Title, preview.Embed!.Type));
    }

    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("not a link")]
    [InlineData("/relative")]
    public async Task Refuses_what_is_not_an_http_link(string url)
    {
        await AuthAsync("links2");
        var response = await _client.GetAsync(PreviewUrl + Uri.EscapeDataString(url));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task A_page_that_cannot_be_read_is_a_404()
    {
        await AuthAsync("links3");
        var response = await _client.GetAsync(PreviewUrl + Uri.EscapeDataString("https://down.example.com/"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private sealed class FakePreviewer : ILinkPreviewer
    {
        public Task<LinkPreview?> PreviewAsync(Uri url, CancellationToken ct) => Task.FromResult(
            url.Host == "down.example.com"
                ? null
                : new LinkPreview(url.AbsoluteUri, "Um post", null, "Example", null, null, new LinkEmbed("rich", "<p>x</p>", null, 500, null)));
    }

    private sealed record EmbedItem(string Type, string? Html);
    private sealed record PreviewItem(string Url, string? Title, EmbedItem? Embed);
    private sealed record Envelope<T>(T Data);
}
