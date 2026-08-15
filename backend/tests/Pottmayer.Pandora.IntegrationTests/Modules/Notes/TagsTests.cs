using System.Net;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

/// <summary>
/// Covers tags as the module defines them: written in the markdown, materialized by the save, and
/// used to narrow the sidebar, the search and the graph. Also the two things the text cannot say —
/// the color, and that a colored tag survives losing its last page.
/// </summary>
[Collection("Integration")]
public sealed class TagsTests : IAsyncLifetime
{
    private const string PagesUrl = "/api/v1/notes/pages";
    private const string TagsUrl = "/api/v1/notes/tags";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TagsTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Requires_authentication()
    {
        var response = await _client.GetAsync(TagsUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Saving_a_page_creates_the_tags_its_body_mentions()
    {
        await AuthAsync("tag1");
        await CreateAsync("Diario", "hoje sobre #ideias e #pandora");

        var tags = await ListTagsAsync();

        Assert.Equal(["ideias", "pandora"], tags.Select(t => t.Slug).OrderBy(s => s));
        Assert.All(tags, t => Assert.Equal(1, t.PageCount));
    }

    [Fact]
    public async Task The_page_carries_the_tags_it_mentions()
    {
        await AuthAsync("tag2");
        var page = await CreateAsync("Diario", "sobre #ideias");

        var tag = Assert.Single((await GetPageAsync(page)).Tags);
        Assert.Equal("ideias", tag.Slug);
    }

    [Fact]
    public async Task Two_spellings_of_the_same_word_are_one_tag()
    {
        await AuthAsync("tag3");
        await CreateAsync("Bebidas", "#Café de manha, #cafe a tarde");

        var tag = Assert.Single(await ListTagsAsync());
        Assert.Equal("cafe", tag.Slug);
        // The display name records how it was first written.
        Assert.Equal("Café", tag.Name);
    }

    [Fact]
    public async Task Removing_the_tag_from_the_text_removes_it_from_the_page()
    {
        await AuthAsync("tag4");
        var page = await CreateAsync("Diario", "sobre #ideias");

        await UpdateAsync(page, "Diario", "sobre coisa nenhuma");

        Assert.Empty((await GetPageAsync(page)).Tags);
        Assert.Empty(await ListTagsAsync());
    }

    [Fact]
    public async Task Saving_the_same_text_twice_changes_nothing()
    {
        await AuthAsync("tag5");
        var page = await CreateAsync("Diario", "sobre #ideias");

        await UpdateAsync(page, "Diario", "sobre #ideias");
        await UpdateAsync(page, "Diario", "sobre #ideias");

        Assert.Single((await GetPageAsync(page)).Tags);
        Assert.Equal(1, Assert.Single(await ListTagsAsync()).PageCount);
    }

    [Fact]
    public async Task A_heading_a_number_and_code_are_not_tags()
    {
        await AuthAsync("tag6");
        await CreateAsync("Variados", "# Titulo\n\nissue #123\n\n```\n#comentario\n```\n\n#valida");

        Assert.Equal(["valida"], (await ListTagsAsync()).Select(t => t.Slug));
    }

    [Fact]
    public async Task Filtering_the_tree_by_tag_returns_only_the_pages_carrying_it()
    {
        await AuthAsync("tag7");
        var withTag = await CreateAsync("Com", "sobre #ideias");
        await CreateAsync("Sem", "nada aqui");

        var ideias = (await ListTagsAsync()).Single(t => t.Slug == "ideias");
        var tree = await ListPagesAsync($"?tagIds={ideias.Id}");

        Assert.Equal(withTag, Assert.Single(tree).Id);
    }

    [Fact]
    public async Task Several_tags_intersect_rather_than_add_up()
    {
        await AuthAsync("tag8");
        var both = await CreateAsync("Ambas", "#ideias e #pandora");
        await CreateAsync("Uma", "so #ideias");

        var tags = await ListTagsAsync();
        var query = string.Join("&", tags.Select(t => $"tagIds={t.Id}"));

        Assert.Equal(both, Assert.Single(await ListPagesAsync($"?{query}")).Id);
    }

    [Fact]
    public async Task Search_can_be_narrowed_by_tag()
    {
        await AuthAsync("tag9");
        var tagged = await CreateAsync("Tagueada", "assunto comum com #ideias");
        await CreateAsync("Solta", "assunto comum sem nada");

        var ideias = (await ListTagsAsync()).Single(t => t.Slug == "ideias");

        Assert.Equal(2, (await SearchAsync("?q=comum")).Count);
        Assert.Equal(tagged, Assert.Single(await SearchAsync($"?q=comum&tagIds={ideias.Id}")).Id);
    }

    [Fact]
    public async Task A_tag_with_no_term_lists_that_tags_pages()
    {
        await AuthAsync("tag10");
        var tagged = await CreateAsync("Tagueada", "sobre #ideias");
        await CreateAsync("Solta", "nada");

        var ideias = (await ListTagsAsync()).Single(t => t.Slug == "ideias");

        Assert.Equal(tagged, Assert.Single(await SearchAsync($"?q=&tagIds={ideias.Id}")).Id);
    }

    [Fact]
    public async Task The_graph_can_be_cut_down_to_a_tag()
    {
        await AuthAsync("tag11");
        var tagged = await CreateAsync("Tagueada", "sobre #ideias");
        await CreateAsync("Solta", "nada");

        var ideias = (await ListTagsAsync()).Single(t => t.Slug == "ideias");
        var graph = await GetGraphAsync($"?tagIds={ideias.Id}");

        Assert.Equal(tagged, Assert.Single(graph.Nodes).Id);
    }

    [Fact]
    public async Task A_tag_that_loses_its_last_page_is_swept_away()
    {
        await AuthAsync("tag12");
        var page = await CreateAsync("Diario", "sobre #efemera");

        await _client.DeleteAsync($"{PagesUrl}/{page}");

        Assert.Empty(await ListTagsAsync());
    }

    [Fact]
    public async Task A_colored_tag_survives_losing_its_last_page()
    {
        await AuthAsync("tag13");
        var page = await CreateAsync("Diario", "sobre #importante");

        var tag = Assert.Single(await ListTagsAsync());
        await SetColorAsync(tag.Id, "#7c3aed");

        await UpdateAsync(page, "Diario", "sem tag nenhuma");

        var kept = Assert.Single(await ListTagsAsync());
        Assert.Equal("#7c3aed", kept.Color);
        Assert.Equal(0, kept.PageCount);
    }

    [Fact]
    public async Task Clearing_the_color_makes_the_tag_sweepable_again()
    {
        await AuthAsync("tag14");
        var page = await CreateAsync("Diario", "sobre #passageira");

        var tag = Assert.Single(await ListTagsAsync());
        await SetColorAsync(tag.Id, "#7c3aed");
        await SetColorAsync(tag.Id, null);

        await UpdateAsync(page, "Diario", "sem tag nenhuma");

        Assert.Empty(await ListTagsAsync());
    }

    [Fact]
    public async Task A_color_that_is_not_a_hex_literal_is_rejected()
    {
        await AuthAsync("tag15");
        await CreateAsync("Diario", "sobre #ideias");
        var tag = Assert.Single(await ListTagsAsync());

        var response = await _client.PutAsJsonAsync($"{TagsUrl}/{tag.Id}/color",
            new { color = "javascript:alert(1)" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Tags_do_not_cross_owners()
    {
        await AuthAsync("tag-owner");
        await CreateAsync("Privada", "sobre #segredo");

        await AuthAsync("tag-intruder");
        Assert.Empty(await ListTagsAsync());
    }

    [Fact]
    public async Task Painting_a_tag_of_another_owner_is_a_404()
    {
        await AuthAsync("tag-owner2");
        await CreateAsync("Privada", "sobre #segredo");
        var tag = Assert.Single(await ListTagsAsync());

        await AuthAsync("tag-intruder2");
        var response = await _client.PutAsJsonAsync($"{TagsUrl}/{tag.Id}/color", new { color = "#000000" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private async Task<Guid> CreateAsync(string title, string contentMarkdown)
    {
        var response = await _client.PostAsJsonAsync(PagesUrl, new { title, contentMarkdown });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Envelope<PageNode>>())!.Data.Id;
    }

    private async Task UpdateAsync(Guid id, string title, string contentMarkdown)
    {
        var response = await _client.PutAsJsonAsync($"{PagesUrl}/{id}", new { title, contentMarkdown });
        response.EnsureSuccessStatusCode();
    }

    private async Task SetColorAsync(Guid id, string? color)
    {
        var response = await _client.PutAsJsonAsync($"{TagsUrl}/{id}/color", new { color });
        response.EnsureSuccessStatusCode();
    }

    private async Task<PageNode> GetPageAsync(Guid id)
        => (await _client.GetFromJsonAsync<Envelope<PageNode>>($"{PagesUrl}/{id}"))!.Data;

    private async Task<IReadOnlyList<TagNode>> ListTagsAsync()
        => (await _client.GetFromJsonAsync<Envelope<IReadOnlyList<TagNode>>>(TagsUrl))!.Data;

    private async Task<IReadOnlyList<PageNode>> ListPagesAsync(string query)
        => (await _client.GetFromJsonAsync<Envelope<IReadOnlyList<PageNode>>>($"{PagesUrl}{query}"))!.Data;

    private async Task<IReadOnlyList<PageNode>> SearchAsync(string query)
        => (await _client.GetFromJsonAsync<Envelope<IReadOnlyList<PageNode>>>($"{PagesUrl}/search{query}"))!.Data;

    private async Task<GraphNode> GetGraphAsync(string query)
        => (await _client.GetFromJsonAsync<Envelope<GraphNode>>($"{PagesUrl}/graph{query}"))!.Data;

    private sealed record Envelope<T>(T Data);
    private sealed record PageNode(Guid Id, string Title, IReadOnlyList<TagNode> Tags);
    private sealed record TagNode(Guid Id, string Slug, string Name, string? Color, int PageCount);
    private sealed record GraphNode(IReadOnlyList<PageNode> Nodes);
}
