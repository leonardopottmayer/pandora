using System.Net;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

/// <summary>
/// Covers folders: nesting in a space, pages placed in a folder (and only top-level ones), moving,
/// cycles refused, and deleting a folder with what it holds.
/// </summary>
[Collection("Integration")]
public sealed class FoldersTests : IAsyncLifetime
{
    private const string FoldersUrl = "/api/v1/notes/folders";
    private const string PagesUrl = "/api/v1/notes/pages";
    private const string SpacesUrl = "/api/v1/notes/spaces";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FoldersTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Folders_nest_in_their_space_and_hold_top_level_pages()
    {
        await AuthAsync("folders1");
        var backend = await CreateFolderAsync(new { name = "Backend" });
        var dotnet = await CreateFolderAsync(new { name = ".NET", parentId = backend.Id });
        var page = await CreatePageAsync(new { title = "Outbox", folderId = dotnet.Id });
        var child = await CreatePageAsync(new { title = "Detalhe", parentId = page.Id, folderId = backend.Id });

        Assert.Equal(backend.SpaceId, dotnet.SpaceId);
        Assert.Equal((dotnet.Id, backend.SpaceId), (page.FolderId!.Value, page.SpaceId));
        // A sub-page lives under its parent, never in a folder of its own.
        Assert.Null(child.FolderId);

        var folders = await ListAsync<FolderNode>($"{FoldersUrl}?spaceId={backend.SpaceId}");
        Assert.Equal([backend.Id, dotnet.Id], folders.Select(f => f.Id).Order());
        Assert.Equal(dotnet.Id, (await ListAsync<PageNode>(PagesUrl)).Single(p => p.Id == page.Id).FolderId);
    }

    [Fact]
    public async Task A_page_moves_into_a_folder_and_out_under_another_page()
    {
        await AuthAsync("folders2");
        var folder = await CreateFolderAsync(new { name = "Leituras" });
        var page = await CreatePageAsync(new { title = "Livro" });
        var other = await CreatePageAsync(new { title = "Índice" });

        var moved = await MoveAsync($"{PagesUrl}/{page.Id}/move", new { parentId = (Guid?)null, orderIndex = 0, folderId = folder.Id });
        Assert.Equal(folder.Id, (await moved.Content.ReadFromJsonAsync<Envelope<PageNode>>())!.Data.FolderId);

        var under = await MoveAsync($"{PagesUrl}/{page.Id}/move", new { parentId = other.Id, orderIndex = 0, folderId = folder.Id });
        var node = (await under.Content.ReadFromJsonAsync<Envelope<PageNode>>())!.Data;
        Assert.Equal((other.Id, (Guid?)null), (node.ParentId!.Value, node.FolderId));
    }

    [Fact]
    public async Task A_folder_cannot_move_into_itself_or_a_subfolder()
    {
        await AuthAsync("folders3");
        var outer = await CreateFolderAsync(new { name = "Fora" });
        var inner = await CreateFolderAsync(new { name = "Dentro", parentId = outer.Id });

        var response = await MoveAsync($"{FoldersUrl}/{outer.Id}/move", new { parentId = inner.Id, orderIndex = 0 });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var ok = await MoveAsync($"{FoldersUrl}/{inner.Id}/move", new { parentId = (Guid?)null, orderIndex = 3 });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_folder_deletes_its_subfolders_and_pages()
    {
        await AuthAsync("folders4");
        var folder = await CreateFolderAsync(new { name = "Velho" });
        var sub = await CreateFolderAsync(new { name = "Sub", parentId = folder.Id });
        var page = await CreatePageAsync(new { title = "Dentro", folderId = sub.Id });
        await CreatePageAsync(new { title = "Filha", parentId = page.Id });
        var kept = await CreatePageAsync(new { title = "Fora" });

        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync($"{FoldersUrl}/{folder.Id}")).StatusCode);

        Assert.Empty(await ListAsync<FolderNode>(FoldersUrl));
        Assert.Equal([kept.Id], (await ListAsync<PageNode>(PagesUrl)).Select(p => p.Id));
    }

    [Fact]
    public async Task A_space_holding_only_folders_is_not_empty()
    {
        await AuthAsync("folders5");
        await CreatePageAsync(new { title = "Default" });
        var spaceResponse = await _client.PostAsJsonAsync(SpacesUrl, new { name = "Trabalho" });
        var space = (await spaceResponse.Content.ReadFromJsonAsync<Envelope<FolderNode>>())!.Data;
        await CreateFolderAsync(new { name = "Projetos", spaceId = space.Id });

        Assert.Equal(HttpStatusCode.Conflict, (await _client.DeleteAsync($"{SpacesUrl}/{space.Id}")).StatusCode);
    }

    [Fact]
    public async Task Folders_of_another_user_are_not_found()
    {
        await AuthAsync("folders-owner");
        var folder = await CreateFolderAsync(new { name = "Meu" });

        await AuthAsync("folders-intruder");
        Assert.Empty(await ListAsync<FolderNode>(FoldersUrl));
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.PutAsJsonAsync($"{FoldersUrl}/{folder.Id}", new { name = "Teu" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.PostAsJsonAsync(PagesUrl, new { title = "x", folderId = folder.Id })).StatusCode);
    }

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private async Task<FolderNode> CreateFolderAsync(object body)
    {
        var response = await _client.PostAsJsonAsync(FoldersUrl, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<FolderNode>>())!.Data;
    }

    private async Task<PageNode> CreatePageAsync(object body)
    {
        var response = await _client.PostAsJsonAsync(PagesUrl, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<PageNode>>())!.Data;
    }

    private Task<HttpResponseMessage> MoveAsync(string url, object body) => _client.PostAsJsonAsync(url, body);

    private async Task<IReadOnlyList<T>> ListAsync<T>(string url)
        => (await _client.GetFromJsonAsync<Envelope<IReadOnlyList<T>>>(url))!.Data;

    private sealed record Envelope<T>(T Data);
    private sealed record FolderNode(Guid Id, Guid SpaceId, Guid? ParentId, string Name);
    private sealed record PageNode(Guid Id, Guid SpaceId, Guid? ParentId, Guid? FolderId, string Title);
}
