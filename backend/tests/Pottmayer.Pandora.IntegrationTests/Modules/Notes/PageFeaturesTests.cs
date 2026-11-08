using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

/// <summary>
/// Covers what a page is besides its blocks: its look, lock and verification, the recent pages, the
/// trash (restore and delete for good) and the public link, read without signing in.
/// </summary>
[Collection("Integration")]
public sealed class PageFeaturesTests : IAsyncLifetime
{
    private const string PagesUrl = "/api/v1/notes/pages";
    private const string TrashUrl = "/api/v1/notes/trash";
    private const string PublicUrl = "/api/v1/public/notes/pages";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PageFeaturesTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Look_lock_and_verification_are_kept_and_bad_values_refused()
    {
        await AuthAsync("features1");
        var page = await CreatePageAsync("Guia");

        var looked = await PutAsync($"{PagesUrl}/{page.Id}/appearance",
            new { cover = new { gradient = "ocean", position = 30 }, width = "wide", font = "serif", smallText = true });
        Assert.Equal(("wide", "serif", true, "ocean"), (looked.Appearance.Width, looked.Appearance.Font, looked.Appearance.SmallText, looked.Appearance.Cover!.Gradient));
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await _client.PutAsJsonAsync($"{PagesUrl}/{page.Id}/appearance", new { width = "huge" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await _client.PutAsJsonAsync($"{PagesUrl}/{page.Id}/appearance", new { cover = new { gradient = "ocean", mediaId = Guid.NewGuid() } })).StatusCode);

        Assert.True((await PutAsync($"{PagesUrl}/{page.Id}/lock", new { locked = true })).IsLocked);

        var verified = await PutAsync($"{PagesUrl}/{page.Id}/verification", new { verified = true, days = 30 });
        Assert.NotNull(verified.VerifiedAt);
        Assert.InRange((verified.VerifiedUntil!.Value - verified.VerifiedAt!.Value).TotalDays, 29.9, 30.1);
        Assert.Null((await PutAsync($"{PagesUrl}/{page.Id}/verification", new { verified = true, days = (int?)null })).VerifiedUntil);
        Assert.Null((await PutAsync($"{PagesUrl}/{page.Id}/verification", new { verified = false })).VerifiedAt);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await _client.PutAsJsonAsync($"{PagesUrl}/{page.Id}/verification", new { verified = true, days = 0 })).StatusCode);
    }

    [Fact]
    public async Task Opening_a_page_puts_it_in_the_recent_ones_without_editing_it()
    {
        await AuthAsync("features2");
        var first = await CreatePageAsync("Primeira");
        var second = await CreatePageAsync("Segunda");
        await CreatePageAsync("Nunca aberta");

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{PagesUrl}/{first.Id}/viewed", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{PagesUrl}/{second.Id}/viewed", null)).StatusCode);

        var recent = (await _client.GetFromJsonAsync<Envelope<List<PageItem>>>($"{PagesUrl}/recent"))!.Data;
        Assert.Equal([second.Id, first.Id], recent.Select(p => p.Id));
        Assert.Null((await GetPageAsync(first.Id)).UpdatedAt);
    }

    [Fact]
    public async Task A_page_comes_back_from_the_trash_with_its_sub_pages_and_a_free_slug()
    {
        await AuthAsync("features3");
        var parent = await CreatePageAsync("Projeto");
        var child = await CreatePageAsync("Detalhe", parent.Id);
        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync($"{PagesUrl}/{parent.Id}")).StatusCode);

        var trash = (await _client.GetFromJsonAsync<Envelope<List<TrashItem>>>(TrashUrl))!.Data;
        Assert.Equal([(parent.Id, 1)], trash.Select(t => (t.Id, t.SubpageCount)));

        // A new page took the slug meanwhile: the restored one gets another.
        await CreatePageAsync("Projeto");
        var restored = await PostAsync<PageItem>($"{TrashUrl}/{parent.Id}/restore");
        Assert.Equal("projeto-2", restored.Slug);
        Assert.Equal(parent.Id, (await GetPageAsync(child.Id)).ParentId);
        Assert.Empty((await _client.GetFromJsonAsync<Envelope<List<TrashItem>>>(TrashUrl))!.Data);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync($"{TrashUrl}/{parent.Id}/restore", null)).StatusCode);
    }

    [Fact]
    public async Task Deleting_for_good_removes_the_page_its_sub_pages_and_their_history()
    {
        await AuthAsync("features4");
        var parent = await CreatePageAsync("Velho");
        await CreatePageAsync("Filho", parent.Id);
        var other = await CreatePageAsync("Outro");
        await _client.DeleteAsync($"{PagesUrl}/{parent.Id}");
        await _client.DeleteAsync($"{PagesUrl}/{other.Id}");

        var gone = await _client.DeleteAsync($"{TrashUrl}/{parent.Id}");
        Assert.Equal(2, (await gone.Content.ReadFromJsonAsync<Envelope<int>>())!.Data);
        Assert.Equal([other.Id], (await _client.GetFromJsonAsync<Envelope<List<TrashItem>>>(TrashUrl))!.Data.Select(t => t.Id));

        var emptied = await _client.DeleteAsync(TrashUrl);
        Assert.Equal(1, (await emptied.Content.ReadFromJsonAsync<Envelope<int>>())!.Data);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync($"{TrashUrl}/{other.Id}/restore", null)).StatusCode);
    }

    [Fact]
    public async Task A_public_link_shows_the_page_and_its_images_to_anyone_until_it_is_closed()
    {
        await AuthAsync("features5");
        var shown = await UploadAsync("shown.png");
        var hidden = await UploadAsync("hidden.png");
        var page = await CreatePageAsync("Público", content: NoteBlocks.Doc(NoteBlocks.Paragraph("Olá"), NoteBlocks.Image(shown)));

        var opened = await PutAsync($"{PagesUrl}/{page.Id}/public", new { @public = true });
        var token = opened.PublicToken!;
        Assert.True(token.Length >= 40);
        Assert.Equal(token, (await PutAsync($"{PagesUrl}/{page.Id}/public", new { @public = true })).PublicToken);

        using var anonymous = _factory.CreateClient();
        var read = await anonymous.GetFromJsonAsync<Envelope<PublicItem>>($"{PublicUrl}/{token}");
        Assert.Equal("Público", read!.Data.Title);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"{PublicUrl}/{token}/media/{shown}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"{PublicUrl}/{token}/media/{hidden}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"{PublicUrl}/not-a-token")).StatusCode);

        Assert.Null((await PutAsync($"{PagesUrl}/{page.Id}/public", new { @public = false })).PublicToken);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"{PublicUrl}/{token}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"{PublicUrl}/{token}/media/{shown}")).StatusCode);
    }

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private async Task<PageItem> CreatePageAsync(string title, Guid? parentId = null, object? content = null)
    {
        var response = await _client.PostAsJsonAsync(PagesUrl, new { title, parentId, content = content ?? NoteBlocks.Doc() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<PageItem>>())!.Data;
    }

    private async Task<PageItem> GetPageAsync(Guid id) => (await _client.GetFromJsonAsync<Envelope<PageItem>>($"{PagesUrl}/{id}"))!.Data;

    private async Task<PageItem> PutAsync(string url, object body)
    {
        var response = await _client.PutAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<PageItem>>())!.Data;
    }

    private async Task<T> PostAsync<T>(string url)
    {
        var response = await _client.PostAsync(url, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<T>>())!.Data;
    }

    private async Task<Guid> UploadAsync(string fileName)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        var response = await _client.PostAsync("/api/v1/notes/media", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data.GetProperty("id").GetGuid();
    }

    private sealed record Cover(Guid? MediaId, string? Gradient, int Position);
    private sealed record Look(Cover? Cover, string Width, string Font, bool SmallText);
    private sealed record PageItem(
        Guid Id, Guid? ParentId, string Title, string Slug, DateTimeOffset? UpdatedAt,
        Look Appearance, bool IsLocked, DateTimeOffset? VerifiedAt, DateTimeOffset? VerifiedUntil, string? PublicToken);
    private sealed record TrashItem(Guid Id, string Title, int SubpageCount);
    private sealed record PublicItem(string Title, JsonElement Content);
    private sealed record Envelope<T>(T Data);
}
