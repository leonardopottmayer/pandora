using System.Net;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

/// <summary>
/// Covers the graph view reads: the global graph of a user's pages and the local graph
/// (neighborhood of one page at a given depth), both built from the <c>PageLink</c> edges the
/// wikilink parser already materializes.
/// </summary>
[Collection("Integration")]
public sealed class GraphTests : IAsyncLifetime
{
    private const string Url = "/api/v1/notes/pages";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GraphTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Global_graph_returns_every_page_and_the_edges_between_them()
    {
        await AuthAsync("graph1");
        var b = await CreateAsync("B");
        var a = await CreateAsync("A", "[[B]]");
        var lonely = await CreateAsync("Lonely");

        var graph = await GraphAsync();

        Assert.Equal(3, graph.Nodes.Count);
        Assert.Contains(graph.Nodes, n => n.Id == lonely && n.Degree == 0);

        var edge = Assert.Single(graph.Edges);
        Assert.Equal(a, edge.SourceId);
        Assert.Equal(b, edge.TargetId);
        Assert.Equal("wikilink", edge.Kind);

        // Both ends of the edge count it, whichever way it points.
        Assert.All(
            graph.Nodes.Where(n => n.Id != lonely),
            n => Assert.Equal(1, n.Degree));
    }

    [Fact]
    public async Task An_embed_and_a_link_to_the_same_page_are_two_edges()
    {
        await AuthAsync("graph2");
        await CreateAsync("B");
        await CreateAsync("A", "[[B]] and ![[B]]");

        var graph = await GraphAsync();

        Assert.Equal(2, graph.Edges.Count);
        Assert.Contains(graph.Edges, e => e.Kind == "wikilink");
        Assert.Contains(graph.Edges, e => e.Kind == "embed");
    }

    [Fact]
    public async Task Local_graph_at_depth_one_is_the_page_and_its_direct_neighbors()
    {
        await AuthAsync("graph3");
        // c <- b <- a  and  d -> b : from b, a and d are one hop, c is one hop, nothing is two.
        var c = await CreateAsync("C");
        var b = await CreateAsync("B", "[[C]]");
        var a = await CreateAsync("A", "[[B]]");
        var d = await CreateAsync("D", "[[B]]");

        var graph = await GraphAsync($"{Url}/{b}/graph?depth=1");

        Assert.Equal(4, graph.Nodes.Count);
        Assert.Contains(graph.Nodes, n => n.Id == a);
        Assert.Contains(graph.Nodes, n => n.Id == c);
        Assert.Contains(graph.Nodes, n => n.Id == d);
    }

    [Fact]
    public async Task Local_graph_excludes_what_is_further_than_the_depth_asked_for()
    {
        await AuthAsync("graph4");
        // a -> b -> c: from a, c is two hops away.
        var c = await CreateAsync("C");
        await CreateAsync("B", "[[C]]");
        var a = await CreateAsync("A", "[[B]]");

        var depthOne = await GraphAsync($"{Url}/{a}/graph?depth=1");
        Assert.Equal(2, depthOne.Nodes.Count);
        Assert.DoesNotContain(depthOne.Nodes, n => n.Id == c);

        var depthTwo = await GraphAsync($"{Url}/{a}/graph?depth=2");
        Assert.Equal(3, depthTwo.Nodes.Count);
        Assert.Contains(depthTwo.Nodes, n => n.Id == c);

        // Edges follow the nodes: at depth 1 the b -> c edge has no endpoint to attach to.
        Assert.Single(depthOne.Edges);
        Assert.Equal(2, depthTwo.Edges.Count);
    }

    [Fact]
    public async Task Local_graph_of_an_unlinked_page_is_the_page_alone()
    {
        await AuthAsync("graph5");
        await CreateAsync("Elsewhere");
        var alone = await CreateAsync("Alone");

        var graph = await GraphAsync($"{Url}/{alone}/graph?depth=3");

        Assert.Equal(alone, Assert.Single(graph.Nodes).Id);
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public async Task Deleting_a_page_takes_it_and_its_edges_out_of_the_graph()
    {
        await AuthAsync("graph6");
        var b = await CreateAsync("B");
        await CreateAsync("A", "[[B]]");

        (await _client.DeleteAsync($"{Url}/{b}")).EnsureSuccessStatusCode();

        var graph = await GraphAsync();
        Assert.Single(graph.Nodes);
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public async Task Archived_pages_stay_in_the_graph_flagged()
    {
        await AuthAsync("graph7");
        var b = await CreateAsync("B");
        await CreateAsync("A", "[[B]]");

        (await _client.PostAsync($"{Url}/{b}/archive", null)).EnsureSuccessStatusCode();

        var graph = await GraphAsync();
        Assert.Equal(2, graph.Nodes.Count);
        Assert.Single(graph.Edges);
        Assert.True(graph.Nodes.Single(n => n.Id == b).IsArchived);
    }

    [Fact]
    public async Task Graph_does_not_cross_owners()
    {
        await AuthAsync("graph-owner");
        await CreateAsync("Mine");

        await AuthAsync("graph-intruder");
        var graph = await GraphAsync();

        Assert.Empty(graph.Nodes);
    }

    [Fact]
    public async Task Local_graph_of_a_foreign_page_returns_not_found()
    {
        await AuthAsync("graph-owner2");
        var page = await CreateAsync("Private");

        await AuthAsync("graph-intruder2");
        Assert.Equal(
            HttpStatusCode.NotFound, (await _client.GetAsync($"{Url}/{page}/graph")).StatusCode);
    }

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private async Task<Guid> CreateAsync(string title, string? contentMarkdown = null)
    {
        var response = await _client.PostAsJsonAsync(Url, new { title, contentMarkdown });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SingleEnvelope>())!.Data.Id;
    }

    private async Task<GraphPayload> GraphAsync(string? url = null)
        => (await _client.GetFromJsonAsync<GraphEnvelope>(url ?? $"{Url}/graph"))!.Data;

    private sealed record SingleEnvelope(PageNode Data);
    private sealed record PageNode(Guid Id);
    private sealed record GraphEnvelope(GraphPayload Data);

    private sealed record GraphPayload(
        IReadOnlyList<NodeDto> Nodes, IReadOnlyList<EdgeDto> Edges);

    private sealed record NodeDto(
        Guid Id, string Title, string Slug, string? Icon, bool IsArchived, int Degree);

    private sealed record EdgeDto(Guid SourceId, Guid TargetId, string Kind);
}
