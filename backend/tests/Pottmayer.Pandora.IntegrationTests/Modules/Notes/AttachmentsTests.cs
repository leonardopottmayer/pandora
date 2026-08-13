using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

/// <summary>
/// Covers attachment upload/download: authenticated round-trip for an image and a zip (with the right
/// Content-Type), the empty-file guard, foreign/unknown lookups, and pinning an upload to a page.
/// </summary>
[Collection("Integration")]
public sealed class AttachmentsTests : IAsyncLifetime
{
    private const string Url = "/api/v1/notes/attachments";
    private const string PagesUrl = "/api/v1/notes/pages";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AttachmentsTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Download_requires_authentication()
    {
        var response = await _client.GetAsync($"{Url}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_and_download_an_image_round_trips_with_content_type()
    {
        await AuthAsync("attach1");

        // A tiny PNG header is enough to prove bytes survive the round-trip untouched.
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
        var upload = await UploadAsync(bytes, "logo.png", "image/png");

        Assert.Equal(HttpStatusCode.OK, upload.status);
        Assert.Equal("logo.png", upload.dto!.FileName);
        Assert.Equal("image/png", upload.dto.ContentType);
        Assert.Equal(bytes.Length, upload.dto.SizeBytes);
        Assert.Equal($"{Url}/{upload.dto.Id}", upload.dto.Url);

        var download = await _client.GetAsync(upload.dto.Url);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Upload_and_download_a_zip_round_trips_with_content_type()
    {
        await AuthAsync("attach2");

        var bytes = Encoding.UTF8.GetBytes("PK not really a zip but bytes are bytes");
        var upload = await UploadAsync(bytes, "bundle.zip", "application/zip");
        Assert.Equal(HttpStatusCode.OK, upload.status);

        var download = await _client.GetAsync(upload.dto!.Url);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/zip", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Empty_file_is_rejected()
    {
        await AuthAsync("attach3");

        var upload = await UploadAsync([], "empty.txt", "text/plain");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, upload.status);
    }

    [Fact]
    public async Task Unknown_attachment_returns_not_found()
    {
        await AuthAsync("attach4");

        var response = await _client.GetAsync($"{Url}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_pinned_to_an_owned_page_succeeds()
    {
        await AuthAsync("attach5");

        var page = await _client.PostAsJsonAsync(PagesUrl, new { title = "Host page" });
        var pageId = (await page.Content.ReadFromJsonAsync<PageEnvelope>())!.Data.Id;

        var upload = await UploadAsync([1, 2, 3], "note.bin", "application/octet-stream", pageId);
        Assert.Equal(HttpStatusCode.OK, upload.status);
        Assert.Equal(pageId, upload.dto!.PageId);
    }

    [Fact]
    public async Task Upload_pinned_to_a_foreign_page_returns_not_found()
    {
        await AuthAsync("attach-owner");
        var page = await _client.PostAsJsonAsync(PagesUrl, new { title = "Theirs" });
        var pageId = (await page.Content.ReadFromJsonAsync<PageEnvelope>())!.Data.Id;

        await AuthAsync("attach-intruder");
        var upload = await UploadAsync([1, 2, 3], "x.bin", "application/octet-stream", pageId);
        Assert.Equal(HttpStatusCode.NotFound, upload.status);
    }

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private async Task<(HttpStatusCode status, AttachmentNode? dto)> UploadAsync(
        byte[] content, string fileName, string contentType, Guid? pageId = null)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        if (pageId is { } id)
            form.Add(new StringContent(id.ToString()), "pageId");

        var response = await _client.PostAsync(Url, form);
        if (response.StatusCode != HttpStatusCode.OK)
            return (response.StatusCode, null);
        return (response.StatusCode, (await response.Content.ReadFromJsonAsync<AttachmentEnvelope>())!.Data);
    }

    private sealed record AttachmentEnvelope(AttachmentNode Data);

    private sealed record AttachmentNode(
        Guid Id, Guid? PageId, string FileName, string ContentType, long SizeBytes, string Url);

    private sealed record PageEnvelope(PageNode Data);
    private sealed record PageNode(Guid Id);
}
