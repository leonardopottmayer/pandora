using System.Net;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

/// <summary>
/// Covers spaces: the default one, pages placed in a space, moving a subtree across spaces, wikilinks
/// preferring the linking page's own space, and the rules that keep a page from pointing at a missing
/// or hidden default space.
/// </summary>
[Collection("Integration")]
public sealed class SpacesTests : IAsyncLifetime
{
    private const string SpacesUrl = "/api/v1/notes/spaces";
    private const string PagesUrl = "/api/v1/notes/pages";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SpacesTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task The_first_space_is_the_default_and_counts_its_pages()
    {
        await AuthAsync("spaces1");

        var home = await CreateSpaceAsync("Home");
        var work = await CreateSpaceAsync("Work", color: "#2dcd88");
        Assert.True(home.IsDefault);
        Assert.False(work.IsDefault);

        await CreatePageAsync(new { title = "Groceries", spaceId = home.Id });
        await CreatePageAsync(new { title = "Unasked" });

        var spaces = await SpacesAsync();
        Assert.Equal(["Home", "Work"], spaces.Select(s => s.Name));
        Assert.Equal(2, spaces[0].PageCount);
        Assert.Equal(0, spaces[1].PageCount);
        Assert.NotNull(spaces[0].LastEditedAt);
    }

    [Fact]
    public async Task A_page_without_a_space_creates_the_default_when_there_is_none()
    {
        await AuthAsync("spaces2");

        var page = await CreatePageAsync(new { title = "First" });

        var space = Assert.Single(await SpacesAsync());
        Assert.True(space.IsDefault);
        Assert.Equal(space.Id, page.SpaceId);
    }

    [Fact]
    public async Task A_child_lives_in_its_parents_space_and_the_tree_filters_by_space()
    {
        await AuthAsync("spaces3");
        var home = await CreateSpaceAsync("Home");
        var work = await CreateSpaceAsync("Work");

        var parent = await CreatePageAsync(new { title = "Projects", spaceId = work.Id });
        var child = await CreatePageAsync(new { title = "Pandora", parentId = parent.Id, spaceId = home.Id });
        await CreatePageAsync(new { title = "Recipes", spaceId = home.Id });

        Assert.Equal(work.Id, child.SpaceId);
        var workTree = await TreeAsync($"?spaceId={work.Id}");
        Assert.Equal(["Pandora", "Projects"], workTree.Select(p => p.Title).Order());
    }

    [Fact]
    public async Task Moving_a_page_to_another_space_takes_its_subtree()
    {
        await AuthAsync("spaces4");
        var home = await CreateSpaceAsync("Home");
        var work = await CreateSpaceAsync("Work");
        var parent = await CreatePageAsync(new { title = "Trip", spaceId = home.Id });
        var child = await CreatePageAsync(new { title = "Packing", parentId = parent.Id });

        var moved = await _client.PostAsJsonAsync($"{PagesUrl}/{parent.Id}/move", new { parentId = (Guid?)null, orderIndex = 0, spaceId = work.Id });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

        var workTree = await TreeAsync($"?spaceId={work.Id}");
        Assert.Contains(workTree, p => p.Id == child.Id && p.ParentId == parent.Id);
        Assert.Empty(await TreeAsync($"?spaceId={home.Id}"));
    }

    [Fact]
    public async Task A_mention_points_at_the_page_it_names_whatever_its_space()
    {
        await AuthAsync("spaces5");
        var home = await CreateSpaceAsync("Home");
        var work = await CreateSpaceAsync("Work");
        var homeMeeting = await CreatePageAsync(new { title = "Meeting", spaceId = home.Id });
        var workMeeting = await CreatePageAsync(new { title = "Meeting", spaceId = work.Id });

        var linking = await CreatePageAsync(new { title = "Standup", spaceId = home.Id, content = NoteBlocks.Text("see ", NoteBlocks.Mention(workMeeting.Id)) });

        Assert.Contains(await BacklinksAsync(workMeeting.Id), b => b.PageId == linking.Id);
        Assert.DoesNotContain(await BacklinksAsync(homeMeeting.Id), b => b.PageId == linking.Id);
    }

    [Fact]
    public async Task The_default_space_cannot_be_archived_or_deleted_and_a_full_one_cannot_be_deleted()
    {
        await AuthAsync("spaces6");
        var home = await CreateSpaceAsync("Home");
        var work = await CreateSpaceAsync("Work");
        await CreatePageAsync(new { title = "Notes", spaceId = work.Id });

        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"{SpacesUrl}/{home.Id}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.DeleteAsync($"{SpacesUrl}/{home.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.DeleteAsync($"{SpacesUrl}/{work.Id}")).StatusCode);

        var empty = await CreateSpaceAsync("Empty");
        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync($"{SpacesUrl}/{empty.Id}")).StatusCode);
        Assert.DoesNotContain(await SpacesAsync(), s => s.Id == empty.Id);
    }

    [Fact]
    public async Task Making_another_space_default_moves_the_mark_and_brings_it_back_from_the_archive()
    {
        await AuthAsync("spaces7");
        var home = await CreateSpaceAsync("Home");
        var work = await CreateSpaceAsync("Work");
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{SpacesUrl}/{work.Id}/archive", null)).StatusCode);

        var promoted = await _client.PostAsync($"{SpacesUrl}/{work.Id}/default", null);
        Assert.Equal(HttpStatusCode.OK, promoted.StatusCode);

        var spaces = await SpacesAsync();
        Assert.True(spaces.Single(s => s.Id == work.Id).IsDefault);
        Assert.False(spaces.Single(s => s.Id == work.Id).IsArchived);
        Assert.False(spaces.Single(s => s.Id == home.Id).IsDefault);
    }

    [Fact]
    public async Task Rejects_a_blank_name_and_a_color_that_is_not_hex()
    {
        await AuthAsync("spaces8");

        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await _client.PostAsJsonAsync(SpacesUrl, new { name = "  " })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await _client.PostAsJsonAsync(SpacesUrl, new { name = "Ok", color = "red" })).StatusCode);
    }

    [Fact]
    public async Task Another_users_space_is_not_found()
    {
        await AuthAsync("spaces-owner");
        var home = await CreateSpaceAsync("Home");

        await AuthAsync("spaces-intruder");
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.PostAsJsonAsync(PagesUrl, new { title = "Sneaky", spaceId = home.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.PutAsJsonAsync($"{SpacesUrl}/{home.Id}", new { name = "Mine" })).StatusCode);
    }

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private async Task<SpaceNode> CreateSpaceAsync(string name, string? color = null)
    {
        var response = await _client.PostAsJsonAsync(SpacesUrl, new { name, color });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<SpaceNode>>())!.Data;
    }

    private async Task<IReadOnlyList<SpaceNode>> SpacesAsync()
        => (await _client.GetFromJsonAsync<Envelope<IReadOnlyList<SpaceNode>>>(SpacesUrl))!.Data;

    private async Task<PageNode> CreatePageAsync(object body)
    {
        var response = await _client.PostAsJsonAsync(PagesUrl, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<PageNode>>())!.Data;
    }

    private async Task<IReadOnlyList<PageNode>> TreeAsync(string qs)
        => (await _client.GetFromJsonAsync<Envelope<IReadOnlyList<PageNode>>>($"{PagesUrl}{qs}"))!.Data;

    private async Task<IReadOnlyList<BacklinkNode>> BacklinksAsync(Guid id)
        => (await _client.GetFromJsonAsync<Envelope<IReadOnlyList<BacklinkNode>>>($"{PagesUrl}/{id}/backlinks"))!.Data;

    private sealed record Envelope<T>(T Data);
    private sealed record SpaceNode(
        Guid Id, string Name, bool IsDefault, bool IsArchived, int PageCount, DateTimeOffset? LastEditedAt);
    private sealed record PageNode(Guid Id, Guid SpaceId, Guid? ParentId, string Title);
    private sealed record BacklinkNode(Guid PageId);
}
