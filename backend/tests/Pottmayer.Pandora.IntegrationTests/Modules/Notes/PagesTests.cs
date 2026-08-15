using System.Net;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

/// <summary>
/// Covers the page endpoints: CRUD, the sidebar tree, reparent (with cycle rejection), owner scoping
/// (404 on foreign resources), the archive lifecycle, favorites, and soft-delete.
/// </summary>
[Collection("Integration")]
public sealed class PagesTests : IAsyncLifetime
{
    private const string Url = "/api/v1/notes/pages";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PagesTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Requires_authentication()
    {
        var response = await _client.GetAsync(Url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_get_and_list()
    {
        await AuthAsync("notes1");

        var created = await CreateAsync(new { title = "First Note", icon = "📝", contentMarkdown = "# hi" });
        Assert.Equal(HttpStatusCode.OK, created.status);
        Assert.Equal("first-note", created.dto!.Slug);

        var get = await _client.GetFromJsonAsync<SingleEnvelope>($"{Url}/{created.dto.Id}");
        Assert.Equal("First Note", get!.Data.Title);
        Assert.Equal("# hi", get.Data.ContentMarkdown);

        var tree = await TreeAsync();
        Assert.Single(tree, p => p.Id == created.dto.Id);
    }

    [Fact]
    public async Task Rejects_blank_title()
    {
        await AuthAsync("notes2");

        var bad = await CreateAsync(new { title = "   " });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.status);
    }

    [Fact]
    public async Task Duplicate_title_gets_a_distinct_slug()
    {
        await AuthAsync("notes3");

        var first = await CreateAsync(new { title = "Journal" });
        var second = await CreateAsync(new { title = "Journal" });

        Assert.Equal("journal", first.dto!.Slug);
        Assert.Equal("journal-2", second.dto!.Slug);
    }

    [Fact]
    public async Task Create_child_and_move_to_root()
    {
        await AuthAsync("notes4");
        var parent = await CreateAsync(new { title = "Parent" });
        var child = await CreateAsync(new { title = "Child", parentId = parent.dto!.Id });
        Assert.Equal(parent.dto.Id, child.dto!.ParentId);

        var moved = await _client.PostAsJsonAsync($"{Url}/{child.dto.Id}/move",
            new { parentId = (Guid?)null, orderIndex = 1 });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        var reloaded = (await moved.Content.ReadFromJsonAsync<SingleEnvelope>())!.Data;
        Assert.Null(reloaded.ParentId);
        Assert.Equal(1, reloaded.OrderIndex);
    }

    [Fact]
    public async Task Move_that_would_create_a_cycle_is_rejected()
    {
        await AuthAsync("notes5");
        var a = await CreateAsync(new { title = "A" });
        var b = await CreateAsync(new { title = "B", parentId = a.dto!.Id });

        // Trying to put A under its own child B is a cycle.
        var response = await _client.PostAsJsonAsync($"{Url}/{a.dto.Id}/move",
            new { parentId = b.dto!.Id, orderIndex = 0 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Move_under_a_foreign_parent_returns_not_found()
    {
        await AuthAsync("notes-owner");
        var mine = await CreateAsync(new { title = "Mine" });

        await AuthAsync("notes-intruder");
        var theirs = await CreateAsync(new { title = "Theirs" });

        // The intruder cannot reparent their page under a page they do not own.
        var response = await _client.PostAsJsonAsync($"{Url}/{theirs.dto!.Id}/move",
            new { parentId = mine.dto!.Id, orderIndex = 0 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Archive_hides_from_default_tree_and_unarchive_restores()
    {
        await AuthAsync("notes6");
        var page = await CreateAsync(new { title = "Temp" });
        var id = page.dto!.Id;

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Url}/{id}/archive", null)).StatusCode);
        Assert.DoesNotContain(await TreeAsync(), p => p.Id == id);
        Assert.Contains(await TreeAsync("?includeArchived=true"), p => p.Id == id);

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Url}/{id}/unarchive", null)).StatusCode);
        Assert.Contains(await TreeAsync(), p => p.Id == id);
    }

    [Fact]
    public async Task Favorite_and_unfavorite_toggle_the_flag()
    {
        await AuthAsync("notes7");
        var page = await CreateAsync(new { title = "Fav" });
        var id = page.dto!.Id;

        await _client.PostAsync($"{Url}/{id}/favorite", null);
        var favorited = await _client.GetFromJsonAsync<SingleEnvelope>($"{Url}/{id}");
        Assert.True(favorited!.Data.IsFavorite);

        await _client.PostAsync($"{Url}/{id}/unfavorite", null);
        var plain = await _client.GetFromJsonAsync<SingleEnvelope>($"{Url}/{id}");
        Assert.False(plain!.Data.IsFavorite);
    }

    [Fact]
    public async Task Delete_soft_removes_the_page_and_its_subtree()
    {
        await AuthAsync("notes8");
        var parent = await CreateAsync(new { title = "Parent" });
        var child = await CreateAsync(new { title = "Child", parentId = parent.dto!.Id });

        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync($"{Url}/{parent.dto.Id}")).StatusCode);

        // Both parent and child are gone from reads.
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{Url}/{parent.dto.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{Url}/{child.dto!.Id}")).StatusCode);
        Assert.Empty(await TreeAsync("?includeArchived=true"));
    }

    [Fact]
    public async Task Icon_round_trips_through_the_save_and_can_be_cleared()
    {
        await AuthAsync("notes-icon");

        var created = await CreateAsync(new { title = "Com icone", icon = "📝", contentMarkdown = "corpo" });
        var id = created.dto!.Id;
        Assert.Equal("📝", created.dto.Icon);

        // The picker saves through the same PUT the autosave uses, icon included.
        await _client.PutAsJsonAsync($"{Url}/{id}",
            new { title = "Com icone", icon = "🚀", contentMarkdown = "corpo" });
        Assert.Equal("🚀", (await _client.GetFromJsonAsync<SingleEnvelope>($"{Url}/{id}"))!.Data.Icon);

        // It reaches the sidebar, which is the whole point of having one.
        Assert.Equal("🚀", Assert.Single(await TreeAsync(), p => p.Id == id).Icon);

        await _client.PutAsJsonAsync($"{Url}/{id}",
            new { title = "Com icone", icon = (string?)null, contentMarkdown = "corpo" });
        Assert.Null((await _client.GetFromJsonAsync<SingleEnvelope>($"{Url}/{id}"))!.Data.Icon);
    }

    [Fact]
    public async Task Foreign_page_returns_not_found()
    {
        await AuthAsync("notes-owner2");
        var page = await CreateAsync(new { title = "Private" });
        var id = page.dto!.Id;

        await AuthAsync("notes-intruder2");
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{Url}/{id}")).StatusCode);
    }

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private async Task<IReadOnlyList<PageSummaryNode>> TreeAsync(string qs = "")
        => (await _client.GetFromJsonAsync<ListEnvelope>($"{Url}{qs}"))!.Data;

    private async Task<(HttpStatusCode status, PageNode? dto)> CreateAsync(object body)
    {
        var response = await _client.PostAsJsonAsync(Url, body);
        if (response.StatusCode != HttpStatusCode.OK)
            return (response.StatusCode, null);
        return (response.StatusCode, (await response.Content.ReadFromJsonAsync<SingleEnvelope>())!.Data);
    }

    private sealed record ListEnvelope(IReadOnlyList<PageSummaryNode> Data);
    private sealed record SingleEnvelope(PageNode Data);

    private sealed record PageNode(
        Guid Id, Guid? ParentId, string Title, string Slug, string ContentMarkdown,
        string? Icon, int OrderIndex, bool IsFavorite, bool IsArchived);

    private sealed record PageSummaryNode(
        Guid Id, Guid? ParentId, string Title, string Slug, string? Icon,
        int OrderIndex, bool IsFavorite, bool IsArchived);
}
