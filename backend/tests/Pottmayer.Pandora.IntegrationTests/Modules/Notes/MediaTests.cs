using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Pottmayer.Pandora.IntegrationTests.Support;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

/// <summary>
/// Covers the media library: uploads measured and thumbnailed (or kept as files), the kinds not taken
/// yet, serving with long-lived caching, renaming, moving and deleting items, and the folder tree —
/// counts, moves without cycles, and a deleted folder handing what it held to its parent.
/// </summary>
[Collection("Integration")]
public sealed class MediaTests : IAsyncLifetime
{
    private const string Url = "/api/v1/notes/media";
    private const string FoldersUrl = "/api/v1/notes/media-folders";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MediaTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Requires_authentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync($"{Url}/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task A_large_image_is_measured_and_gets_a_webp_thumbnail_both_cached_for_good()
    {
        await AuthAsync("media1");
        var png = Png(800, 600);

        var item = await UploadAsync(png, "beach.png", "image/png");

        Assert.Equal("image", item.Kind);
        Assert.Equal("beach.png", item.Name);
        Assert.Equal((800, 600), (item.Width, item.Height));
        Assert.Equal($"{Url}/{item.Id}", item.Url);
        Assert.Equal($"{Url}/{item.Id}/thumbnail", item.ThumbnailUrl);

        var original = await _client.GetAsync(item.Url);
        Assert.Equal("image/png", original.Content.Headers.ContentType!.MediaType);
        Assert.Equal(png, await original.Content.ReadAsByteArrayAsync());
        Assert.Contains("immutable", original.Headers.CacheControl!.ToString());

        var thumbnail = await _client.GetAsync(item.ThumbnailUrl);
        Assert.Equal("image/webp", thumbnail.Content.Headers.ContentType!.MediaType);
        using var decoded = Image.Load(await thumbnail.Content.ReadAsByteArrayAsync());
        Assert.Equal((400, 300), (decoded.Width, decoded.Height));
    }

    [Fact]
    public async Task A_small_image_needs_no_thumbnail()
    {
        await AuthAsync("media2");

        var item = await UploadAsync(Png(120, 40), "icon.png", "image/png");

        Assert.Equal((120, 40), (item.Width, item.Height));
        Assert.Null(item.ThumbnailUrl);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{Url}/{item.Id}/thumbnail")).StatusCode);
    }

    [Fact]
    public async Task A_file_and_an_unreadable_image_are_kept_as_files()
    {
        await AuthAsync("media3");

        var pdf = await UploadAsync(Encoding.UTF8.GetBytes("%PDF-1.7 not really"), "receipt.pdf", "application/pdf");
        var broken = await UploadAsync(Encoding.UTF8.GetBytes("not an image"), "broken.png", "image/png");

        Assert.Equal(("file", (int?)null), (pdf.Kind, pdf.Width));
        Assert.Equal("file", broken.Kind);
        Assert.Null(broken.ThumbnailUrl);
    }

    [Fact]
    public async Task A_video_and_an_empty_file_are_refused()
    {
        await AuthAsync("media4");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostFileAsync([1, 2, 3], "clip.mp4", "video/mp4")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostFileAsync([], "empty.png", "image/png")).StatusCode);
    }

    [Fact]
    public async Task Items_are_listed_by_folder_kind_and_name()
    {
        await AuthAsync("media5");
        var trips = await CreateFolderAsync("Trips");
        var beach = await UploadAsync(Png(10, 10), "beach.png", "image/png", trips.Id);
        var receipt = await UploadAsync(Encoding.UTF8.GetBytes("x"), "receipt.pdf", "application/pdf");

        Assert.Equal([beach.Id], (await ListAsync($"?folderId={trips.Id}")).Select(i => i.Id));
        Assert.Equal([receipt.Id], (await ListAsync("?unsorted=true")).Select(i => i.Id));
        Assert.Equal([receipt.Id, beach.Id], (await ListAsync("")).Select(i => i.Id));
        Assert.Equal([beach.Id], (await ListAsync("?kind=image")).Select(i => i.Id));
        Assert.Equal([receipt.Id], (await ListAsync("?q=RECEI")).Select(i => i.Id));
    }

    [Fact]
    public async Task An_item_is_renamed_moved_and_deleted_while_its_bytes_keep_serving()
    {
        await AuthAsync("media6");
        var folder = await CreateFolderAsync("Docs");
        var item = await UploadAsync(Encoding.UTF8.GetBytes("x"), "scan.pdf", "application/pdf");

        var renamed = await (await _client.PutAsJsonAsync($"{Url}/{item.Id}", new { name = "Passport scan" }))
            .Content.ReadFromJsonAsync<Envelope<MediaNode>>();
        Assert.Equal(("Passport scan", "scan.pdf"), (renamed!.Data.Name, renamed.Data.OriginalFileName));

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync($"{Url}/{item.Id}/move", new { folderId = folder.Id })).StatusCode);
        Assert.Equal([item.Id], (await ListAsync($"?folderId={folder.Id}")).Select(i => i.Id));

        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync($"{Url}/{item.Id}")).StatusCode);
        Assert.Empty(await ListAsync(""));
        // A note may still embed it, so the bytes stay reachable.
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(item.Url)).StatusCode);
    }

    [Fact]
    public async Task Folders_count_their_items_refuse_cycles_and_hand_their_contents_up_when_deleted()
    {
        await AuthAsync("media7");
        var trips = await CreateFolderAsync("Trips");
        var japan = await CreateFolderAsync("Japan", trips.Id);
        var kyoto = await CreateFolderAsync("Kyoto", japan.Id);
        var photo = await UploadAsync(Png(10, 10), "temple.png", "image/png", japan.Id);
        await UploadAsync(Png(10, 10), "loose.png", "image/png");

        var tree = await FoldersAsync();
        Assert.Equal((1, 2), (tree.UnsortedCount, tree.TotalCount));
        Assert.Equal(1, tree.Folders.Single(f => f.Id == japan.Id).ItemCount);

        Assert.Equal(HttpStatusCode.Conflict,
            (await _client.PostAsJsonAsync($"{FoldersUrl}/{trips.Id}/move", new { parentId = kyoto.Id })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync($"{FoldersUrl}/{japan.Id}")).StatusCode);

        tree = await FoldersAsync();
        Assert.DoesNotContain(tree.Folders, f => f.Id == japan.Id);
        Assert.Equal(trips.Id, tree.Folders.Single(f => f.Id == kyoto.Id).ParentId);
        Assert.Equal([photo.Id], (await ListAsync($"?folderId={trips.Id}")).Select(i => i.Id));
    }

    [Fact]
    public async Task Another_users_media_and_folders_are_not_found()
    {
        await AuthAsync("media-owner");
        var folder = await CreateFolderAsync("Mine");
        var item = await UploadAsync(Png(10, 10), "mine.png", "image/png");

        await AuthAsync("media-intruder");
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(item.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"{Url}/{item.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostFileAsync([1], "x.pdf", "application/pdf", folder.Id)).StatusCode);
        Assert.Empty(await ListAsync(""));
    }

    private static byte[] Png(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(30, 120, 200));
        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return output.ToArray();
    }

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private async Task<HttpResponseMessage> PostFileAsync(byte[] content, string fileName, string contentType, Guid? folderId = null)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        if (folderId is { } id)
            form.Add(new StringContent(id.ToString()), "folderId");
        return await _client.PostAsync(Url, form);
    }

    private async Task<MediaNode> UploadAsync(byte[] content, string fileName, string contentType, Guid? folderId = null)
    {
        var response = await PostFileAsync(content, fileName, contentType, folderId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<MediaNode>>())!.Data;
    }

    private async Task<IReadOnlyList<MediaNode>> ListAsync(string qs)
        => (await _client.GetFromJsonAsync<Envelope<IReadOnlyList<MediaNode>>>($"{Url}{qs}"))!.Data;

    private async Task<FolderNode> CreateFolderAsync(string name, Guid? parentId = null)
    {
        var response = await _client.PostAsJsonAsync(FoldersUrl, new { name, parentId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<FolderNode>>())!.Data;
    }

    private async Task<FoldersNode> FoldersAsync()
        => (await _client.GetFromJsonAsync<Envelope<FoldersNode>>(FoldersUrl))!.Data;

    private sealed record Envelope<T>(T Data);
    private sealed record MediaNode(
        Guid Id, Guid? FolderId, string Kind, string Name, string OriginalFileName,
        int? Width, int? Height, string Url, string? ThumbnailUrl);
    private sealed record FolderNode(Guid Id, Guid? ParentId, string Name, int ItemCount);
    private sealed record FoldersNode(IReadOnlyList<FolderNode> Folders, int UnsortedCount, int TotalCount);
}
