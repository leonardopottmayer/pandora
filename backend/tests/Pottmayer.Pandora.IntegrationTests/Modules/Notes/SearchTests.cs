using System.Net;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

/// <summary>
/// Covers the full-text search behind the command palette: hits on the title and on the body, the
/// prefix match a palette needs while the user is still typing, owner scoping, and the excerpt.
/// </summary>
[Collection("Integration")]
public sealed class SearchTests : IAsyncLifetime
{
    private const string Url = "/api/v1/notes/pages";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SearchTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Requires_authentication()
    {
        var response = await _client.GetAsync($"{Url}/search?q=anything");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Finds_a_page_by_a_word_of_its_body()
    {
        await AuthAsync("search1");
        var page = await CreateAsync("Diario", "Comprei um teclado mecanico ontem.");
        await CreateAsync("Outra", "nada a ver");

        var hit = Assert.Single(await SearchAsync("mecanico"));
        Assert.Equal(page, hit.Id);
        Assert.Equal("Diario", hit.Title);
    }

    [Fact]
    public async Task Finds_a_page_by_its_title()
    {
        await AuthAsync("search2");
        var page = await CreateAsync("Reuniao Semanal", "corpo qualquer");

        var hit = Assert.Single(await SearchAsync("semanal"));
        Assert.Equal(page, hit.Id);
    }

    [Fact]
    public async Task Matches_a_prefix_so_the_palette_answers_while_typing()
    {
        await AuthAsync("search3");
        var page = await CreateAsync("Orcamento", "planejamento do mes");

        Assert.Equal(page, Assert.Single(await SearchAsync("orca")).Id);
        Assert.Equal(page, Assert.Single(await SearchAsync("planej")).Id);
    }

    [Fact]
    public async Task Every_word_of_the_term_must_match()
    {
        await AuthAsync("search4");
        await CreateAsync("Viagem", "roteiro de lisboa");

        Assert.Single(await SearchAsync("roteiro lisboa"));
        Assert.Empty(await SearchAsync("roteiro madrid"));
    }

    [Fact]
    public async Task Search_is_case_insensitive()
    {
        await AuthAsync("search5");
        await CreateAsync("Estudos", "Kubernetes e Docker");

        Assert.Single(await SearchAsync("KUBERNETES"));
    }

    [Fact]
    public async Task Archived_pages_still_show_up_flagged_as_archived()
    {
        await AuthAsync("search6");
        var page = await CreateAsync("Antigo", "assunto encerrado");
        (await _client.PostAsync($"{Url}/{page}/archive", null)).EnsureSuccessStatusCode();

        var hit = Assert.Single(await SearchAsync("encerrado"));
        Assert.True(hit.IsArchived);
    }

    [Fact]
    public async Task Deleted_pages_are_gone_from_the_results()
    {
        await AuthAsync("search7");
        var page = await CreateAsync("Rascunho", "texto descartavel");

        await _client.DeleteAsync($"{Url}/{page}");

        Assert.Empty(await SearchAsync("descartavel"));
    }

    [Fact]
    public async Task Editing_a_page_updates_what_it_matches()
    {
        await AuthAsync("search8");
        var page = await CreateAsync("Notas", "conteudo original");

        await _client.PutAsJsonAsync($"{Url}/{page}",
            new { title = "Notas", contentMarkdown = "conteudo revisado" });

        Assert.Empty(await SearchAsync("original"));
        Assert.Single(await SearchAsync("revisado"));
    }

    [Fact]
    public async Task Results_do_not_cross_owners()
    {
        await AuthAsync("search-owner");
        await CreateAsync("Privado", "segredo absoluto");

        await AuthAsync("search-intruder");
        Assert.Empty(await SearchAsync("segredo"));
    }

    [Fact]
    public async Task A_term_with_nothing_searchable_returns_an_empty_list()
    {
        await AuthAsync("search-blank");
        await CreateAsync("Alguma", "coisa");

        foreach (var term in new[] { "", "   ", "!!!" })
            Assert.Empty(await SearchAsync(term));
    }

    [Fact]
    public async Task Result_carries_an_excerpt_of_the_body_around_the_match()
    {
        await AuthAsync("search9");
        await CreateAsync("Longa", new string('a', 400) + " agulha " + new string('b', 400));

        var hit = Assert.Single(await SearchAsync("agulha"));
        Assert.Contains("agulha", hit.Excerpt);
        Assert.StartsWith("...", hit.Excerpt);
    }

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private async Task<Guid> CreateAsync(string title, string contentMarkdown)
    {
        var response = await _client.PostAsJsonAsync(Url, new { title, contentMarkdown });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SingleEnvelope>())!.Data.Id;
    }

    private async Task<IReadOnlyList<SearchHit>> SearchAsync(string term)
        => (await _client.GetFromJsonAsync<ListEnvelope>($"{Url}/search?q={Uri.EscapeDataString(term)}"))!.Data;

    private sealed record SingleEnvelope(PageNode Data);
    private sealed record PageNode(Guid Id);
    private sealed record ListEnvelope(IReadOnlyList<SearchHit> Data);

    private sealed record SearchHit(
        Guid Id, string Title, string Slug, string? Icon, bool IsArchived, string Excerpt);
}
