using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

/// <summary>
/// Covers the wiki graph: wikilinks parsed out of a page's markdown on save, the edges rebuilt
/// idempotently, and the backlinks ("linked mentions") read — including what happens to edges when a
/// page on either end is deleted.
/// </summary>
[Collection("Integration")]
public sealed class BacklinksTests : IAsyncLifetime
{
    private const string Url = "/api/v1/notes/pages";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public BacklinksTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Link_written_in_a_page_shows_up_as_a_backlink_on_the_target()
    {
        await AuthAsync("links1");
        var target = await CreateAsync("B");
        var source = await CreateAsync("A");

        await UpdateAsync(source, "A", "See [[B]] for details.");

        var backlink = Assert.Single(await BacklinksAsync(target));
        Assert.Equal(source, backlink.PageId);
        Assert.Equal("A", backlink.Title);
        Assert.Equal("wikilink", backlink.Kind);
    }

    [Fact]
    public async Task Removing_the_link_removes_the_edge_and_resaving_does_not_duplicate()
    {
        await AuthAsync("links2");
        var target = await CreateAsync("B");
        var source = await CreateAsync("A");

        await UpdateAsync(source, "A", "[[B]]");
        await UpdateAsync(source, "A", "[[B]] still [[B]]");
        Assert.Single(await BacklinksAsync(target));

        await UpdateAsync(source, "A", "nothing here anymore");
        Assert.Empty(await BacklinksAsync(target));
    }

    [Fact]
    public async Task Target_resolves_by_title_or_by_slug_and_an_embed_is_its_own_edge()
    {
        await AuthAsync("links3");
        var target = await CreateAsync("Meeting Notes");
        var source = await CreateAsync("Journal");

        // Both spellings point at the same page — one edge — and the embed adds a second kind.
        await UpdateAsync(source, "Journal", "[[Meeting Notes]] and [[meeting-notes]] and ![[Meeting Notes]]");

        var backlinks = await BacklinksAsync(target);
        Assert.Equal(2, backlinks.Count);
        Assert.All(backlinks, b => Assert.Equal(source, b.PageId));
        Assert.Contains(backlinks, b => b.Kind == "wikilink");
        Assert.Contains(backlinks, b => b.Kind == "embed");
    }

    [Fact]
    public async Task Link_to_a_page_that_does_not_exist_creates_no_edge()
    {
        await AuthAsync("links4");
        var source = await CreateAsync("A");

        var response = await UpdateRawAsync(source, "A", "[[Nowhere]]");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await BacklinksAsync(source));
    }

    [Fact]
    public async Task Links_do_not_cross_owners()
    {
        await AuthAsync("links-owner");
        var mine = await CreateAsync("Shared Title");

        await AuthAsync("links-intruder");
        var theirs = await CreateAsync("Their Page");
        await UpdateAsync(theirs, "Their Page", "[[Shared Title]]");

        // The intruder's link resolved to nothing: the title belongs to another user's page.
        await ReAuthAsync("links-owner");
        Assert.Empty(await BacklinksAsync(mine));
    }

    [Fact]
    public async Task Content_created_with_wikilinks_is_parsed_on_create()
    {
        await AuthAsync("links5");
        var target = await CreateAsync("B");
        var source = await CreateAsync("A", "[[B]]");

        var backlink = Assert.Single(await BacklinksAsync(target));
        Assert.Equal(source, backlink.PageId);
    }

    [Fact]
    public async Task Deleting_the_source_drops_its_edges()
    {
        await AuthAsync("links6");
        var target = await CreateAsync("B");
        var source = await CreateAsync("A");
        await UpdateAsync(source, "A", "[[B]]");

        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync($"{Url}/{source}")).StatusCode);
        Assert.Empty(await BacklinksAsync(target));
    }

    [Fact]
    public async Task Deleting_the_target_makes_its_backlinks_unreachable()
    {
        await AuthAsync("links7");
        var target = await CreateAsync("B");
        var source = await CreateAsync("A");
        await UpdateAsync(source, "A", "[[B]]");

        await _client.DeleteAsync($"{Url}/{target}");

        // The edge is broken: the target is gone, so its backlinks cannot be read at all.
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{Url}/{target}/backlinks")).StatusCode);

        // Re-saving the source with the same text drops the now-unresolvable edge.
        var response = await UpdateRawAsync(source, "A", "[[B]]");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Backlinks_of_a_foreign_page_return_not_found()
    {
        await AuthAsync("links-owner2");
        var page = await CreateAsync("Private");

        await AuthAsync("links-intruder2");
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{Url}/{page}/backlinks")).StatusCode);
    }

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    /// <summary>Signs back in as a user this test already registered (sign-up would fail a second time).</summary>
    private async Task ReAuthAsync(string username)
    {
        var tokens = await IdentityHelper.SignInAsync(_client, username);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private async Task<Guid> CreateAsync(string title, string? contentMarkdown = null)
    {
        var response = await _client.PostAsJsonAsync(Url, new { title, contentMarkdown });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SingleEnvelope>())!.Data.Id;
    }

    private async Task UpdateAsync(Guid id, string title, string contentMarkdown)
        => (await UpdateRawAsync(id, title, contentMarkdown)).EnsureSuccessStatusCode();

    private Task<HttpResponseMessage> UpdateRawAsync(Guid id, string title, string contentMarkdown)
        => _client.PutAsJsonAsync($"{Url}/{id}", new { title, contentMarkdown });

    private async Task<IReadOnlyList<BacklinkNode>> BacklinksAsync(Guid id)
        => (await _client.GetFromJsonAsync<ListEnvelope>($"{Url}/{id}/backlinks"))!.Data;

    private sealed record SingleEnvelope(PageNode Data);
    private sealed record PageNode(Guid Id);
    private sealed record ListEnvelope(IReadOnlyList<BacklinkNode> Data);

    private sealed record BacklinkNode(
        Guid PageId, string Title, string Slug, string? Icon, bool IsArchived, string Kind);
}
