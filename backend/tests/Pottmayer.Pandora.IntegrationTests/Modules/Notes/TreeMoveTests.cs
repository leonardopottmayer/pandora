using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

/// <summary>Atomic tree drops through existing movement rules, permissions and history.</summary>
[Collection("Integration")]
public sealed class TreeMoveTests(PandoraWebApplicationFactory factory) : IAsyncLifetime
{
    private const string Pages = "/api/v1/notes/pages";
    private const string Folders = "/api/v1/notes/folders";
    private const string Move = "/api/v1/notes/tree/move";
    private readonly HttpClient _client = factory.CreateClient();
    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Page_order_and_destinations_survive_reload_with_children_and_content_intact()
    {
        await AuthAsync("tree-pages");
        var folder = await CreateAsync(Folders, new { name = "Work" });
        var a = await CreateAsync(Pages, new { title = "A", content = NoteBlocks.Text("Keep me") });
        var b = await CreateAsync(Pages, new { title = "B" });
        var c = await CreateAsync(Pages, new { title = "C" });
        var child = await CreateAsync(Pages, new { title = "Child", parentId = a.Id });
        await DropAsync(a.SpaceId, "page", [c.Id, a.Id, b.Id]);
        var roots = (await ListAsync(Pages)).Where(p => p.ParentId is null).ToList();
        Assert.Equal([c.Id, a.Id, b.Id], roots.Select(p => p.Id));
        Assert.Equal([0, 1, 2], roots.Select(p => p.OrderIndex));
        await DropAsync(a.SpaceId, "page", [b.Id, a.Id], folderId: folder.Id);
        Assert.Equal(folder.Id, (await GetAsync(a.Id)).FolderId);
        await DropAsync(a.SpaceId, "page", [a.Id], parentId: b.Id);
        var moved = await GetAsync(a.Id);
        Assert.Equal(b.Id, moved.ParentId);
        Assert.Null(moved.FolderId);
        Assert.Equal(a.Id, (await GetAsync(child.Id)).ParentId);
        Assert.Contains("Keep me", moved.Content.GetRawText());
    }

    [Fact]
    public async Task Folder_order_and_reparenting_preserve_the_subtree()
    {
        await AuthAsync("tree-folders");
        var a = await CreateAsync(Folders, new { name = "A" });
        var b = await CreateAsync(Folders, new { name = "B" });
        var child = await CreateAsync(Folders, new { name = "Child", parentId = a.Id });
        var page = await CreateAsync(Pages, new { title = "In child", folderId = child.Id });
        await DropAsync(a.SpaceId, "folder", [b.Id, a.Id]);
        Assert.Equal([b.Id, a.Id], (await ListAsync(Folders)).Where(f => f.ParentId is null).Select(f => f.Id));
        await DropAsync(a.SpaceId, "folder", [a.Id], parentId: b.Id);
        var folders = await ListAsync(Folders);
        Assert.Equal(b.Id, folders.Single(f => f.Id == a.Id).ParentId);
        Assert.Equal(a.Id, folders.Single(f => f.Id == child.Id).ParentId);
        Assert.Equal(child.Id, (await GetAsync(page.Id)).FolderId);
    }

    [Theory]
    [InlineData("page")]
    [InlineData("folder")]
    public async Task A_late_cycle_rolls_back_all_earlier_moves_and_page_versions(string kind)
    {
        await AuthAsync("tree-rollback");
        var url = kind == "page" ? Pages : Folders;
        var a = await CreateAsync(url, new { title = "A", name = "A" });
        var b = await CreateAsync(url, new { title = "B", name = "B" });
        var before = (await ListAsync(url)).Select(p => (p.Id, p.ParentId, p.OrderIndex)).ToList();
        var response = await _client.PostAsJsonAsync(Move, new { spaceId = a.SpaceId, kind, parentId = b.Id, itemIds = new[] { a.Id, b.Id } });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(before, (await ListAsync(url)).Select(p => (p.Id, p.ParentId, p.OrderIndex)).ToList());
        if (kind == "page")
        {
            var versions = await _client.GetFromJsonAsync<Envelope<JsonElement>>($"{Pages}/{a.Id}/versions");
            Assert.Single(versions!.Data.EnumerateArray());
        }
    }

    [Fact]
    public async Task Foreign_items_and_destinations_in_another_space_are_rejected_without_changes()
    {
        await AuthAsync("tree-owner");
        var page = await CreateAsync(Pages, new { title = "Mine" });
        var anotherSpace = await CreateAsync("/api/v1/notes/spaces", new { name = "Another space" });
        var folder = await CreateAsync(Folders, new { name = "Other space folder", spaceId = anotherSpace.Id });
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync(Move,
            new { spaceId = page.SpaceId, kind = "page", folderId = folder.Id, itemIds = new[] { page.Id } })).StatusCode);
        using var other = factory.CreateClient();
        await IdentityHelper.AuthenticateAsync(other, factory.ConnectionString, "tree-other@example.com", "tree-other");
        var foreign = await other.PostAsJsonAsync(Pages, new { title = "Foreign" });
        var foreignId = (await foreign.Content.ReadFromJsonAsync<Envelope<Node>>())!.Data.Id;
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync(Move,
            new { spaceId = page.SpaceId, kind = "page", itemIds = new[] { page.Id, foreignId } })).StatusCode);
        Assert.Null((await GetAsync(page.Id)).FolderId);
        Assert.Null((await GetAsync(page.Id)).ParentId);
    }

    [Fact]
    public async Task Invalid_kinds_duplicates_and_ambiguous_destinations_are_refused()
    {
        await AuthAsync("tree-invalid");
        var page = await CreateAsync(Pages, new { title = "A" });
        foreach (var body in new object[] {
            new { spaceId = page.SpaceId, kind = "unknown", itemIds = new[] { page.Id } },
            new { spaceId = page.SpaceId, kind = "page", itemIds = new[] { page.Id, page.Id } },
            new { spaceId = page.SpaceId, kind = "page", parentId = page.Id, folderId = Guid.NewGuid(), itemIds = new[] { page.Id } },
            new { spaceId = page.SpaceId, kind = "folder", folderId = Guid.NewGuid(), itemIds = new[] { page.Id } },
            new { spaceId = page.SpaceId, kind = "page", itemIds = Array.Empty<Guid>() } })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.PostAsJsonAsync(Move, body)).StatusCode);
    }

    [Theory]
    [InlineData("page", "notes.pages.update")]
    [InlineData("folder", "notes.folders.update")]
    [InlineData("page", "notes.spaces.read")]
    public async Task Moving_enforces_the_kind_permission_before_persisting(string kind, string permission)
    {
        await AuthAsync("tree-denied");
        var item = await CreateAsync(kind == "page" ? Pages : Folders, new { title = "A", name = "A" });
        var me = await _client.GetFromJsonAsync<Envelope<JsonElement>>("/api/v1/identity/me");
        var userId = me!.Data.GetProperty("id").GetGuid();
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("INSERT INTO admin.adm005_user_permission (user_id, pattern, effect) VALUES ($1, $2, 'deny')", connection);
        command.Parameters.AddWithValue(userId);
        command.Parameters.AddWithValue(permission);
        await command.ExecuteNonQueryAsync();
        factory.Services.GetRequiredService<IPermissionChecker>().Invalidate(userId.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync(Move,
            new { spaceId = item.SpaceId, kind, itemIds = new[] { item.Id } })).StatusCode);
    }

    private Task AuthAsync(string user) => IdentityHelper.AuthenticateAsync(_client, factory.ConnectionString, $"{user}@example.com", user);
    private async Task<Node> CreateAsync(string url, object body)
    {
        var response = await _client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<Node>>())!.Data;
    }
    private async Task<List<Node>> ListAsync(string url) => (await _client.GetFromJsonAsync<Envelope<List<Node>>>(url))!.Data;
    private async Task<Node> GetAsync(Guid id) => (await _client.GetFromJsonAsync<Envelope<Node>>($"{Pages}/{id}"))!.Data;
    private async Task DropAsync(Guid spaceId, string kind, Guid[] itemIds, Guid? parentId = null, Guid? folderId = null) =>
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync(Move, new { spaceId, kind, parentId, folderId, itemIds })).StatusCode);
    private sealed record Node(Guid Id, Guid SpaceId, Guid? ParentId, Guid? FolderId, int OrderIndex, JsonElement Content);
    private sealed record Envelope<T>(T Data);
}
