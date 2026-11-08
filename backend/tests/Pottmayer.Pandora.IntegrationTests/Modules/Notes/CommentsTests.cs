using System.Net;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

/// <summary>Comments on a page's blocks (threads, replies, resolve, delete) and the dates of each block.</summary>
[Collection("Integration")]
public sealed class CommentsTests : IAsyncLifetime
{
    private const string PagesUrl = "/api/v1/notes/pages";
    private const string CommentsUrl = "/api/v1/notes/comments";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CommentsTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_thread_takes_replies_is_resolved_and_goes_with_them()
    {
        await AuthAsync("comments1");
        var page = await CreatePageAsync(new { id = "b1", type = "paragraph", content = new[] { new { text = "Prazo em março" } } });

        var thread = await SendAsync<CommentItem>(HttpMethod.Post, $"{PagesUrl}/{page}/comments", new { blockId = "b1", quote = "março", body = "Não seria abril?" });
        Assert.Equal(("b1", "março", (Guid?)null), (thread.BlockId, thread.Quote, thread.ThreadId));
        var reply = await SendAsync<CommentItem>(HttpMethod.Post, $"{CommentsUrl}/{thread.Id}/replies", new { body = "Abril, sim." });
        Assert.Equal(thread.Id, reply.ThreadId);

        // A reply is answered on its thread, and only a thread is resolved.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.PostAsJsonAsync($"{CommentsUrl}/{reply.Id}/replies", new { body = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.PutAsJsonAsync($"{CommentsUrl}/{reply.Id}/resolved", new { resolved = true })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.PostAsJsonAsync($"{PagesUrl}/{page}/comments", new { blockId = "b1", body = " " })).StatusCode);

        var edited = await SendAsync<CommentItem>(HttpMethod.Put, $"{CommentsUrl}/{reply.Id}", new { body = "Abril, confirmado." });
        Assert.NotNull(edited.EditedAt);
        Assert.NotNull((await SendAsync<CommentItem>(HttpMethod.Put, $"{CommentsUrl}/{thread.Id}/resolved", new { resolved = true })).ResolvedAt);

        var all = await ListAsync(page);
        Assert.Equal(["Não seria abril?", "Abril, confirmado."], all.Select(c => c.Body));

        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync($"{CommentsUrl}/{thread.Id}")).StatusCode);
        Assert.Empty(await ListAsync(page));
    }

    [Fact]
    public async Task Someone_else_neither_reads_nor_answers_the_comments()
    {
        await AuthAsync("comments2");
        var page = await CreatePageAsync(new { id = "b1", type = "paragraph" });
        var thread = await SendAsync<CommentItem>(HttpMethod.Post, $"{PagesUrl}/{page}/comments", new { blockId = "b1", body = "Meu" });

        await AuthAsync("comments3");
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{PagesUrl}/{page}/comments")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync($"{CommentsUrl}/{thread.Id}/replies", new { body = "Intruso" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"{CommentsUrl}/{thread.Id}")).StatusCode);
    }

    [Fact]
    public async Task Each_block_keeps_when_it_was_written_and_last_changed()
    {
        await AuthAsync("comments4");
        var page = await CreatePageAsync(
            new { id = "a", type = "paragraph", content = new[] { new { text = "Fica" } } },
            new { id = "b", type = "paragraph", content = new[] { new { text = "Muda" } } },
            new { id = "c", type = "paragraph", content = new[] { new { text = "Sai" } } });
        var first = (await StampsAsync(page)).ToDictionary(s => s.BlockId);
        Assert.Equal(["a", "b", "c"], first.Keys.Order());

        var saved = await _client.PutAsJsonAsync($"{PagesUrl}/{page}", new
        {
            title = "Página",
            content = NoteBlocks.Doc(
                new { id = "a", type = "paragraph", content = new[] { new { text = "Fica" } } },
                new { id = "b", type = "paragraph", content = new[] { new { text = "Mudou" } } },
                new { id = "d", type = "paragraph", content = new[] { new { text = "Nova" } } })
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var second = (await StampsAsync(page)).ToDictionary(s => s.BlockId);
        Assert.Equal(["a", "b", "d"], second.Keys.Order());
        Assert.Equal(first["a"].UpdatedAt, second["a"].UpdatedAt);
        Assert.Equal(first["b"].CreatedAt, second["b"].CreatedAt);
        Assert.True(second["b"].UpdatedAt > first["b"].UpdatedAt);
        Assert.Equal("edit", second["b"].UpdatedSource);
    }

    [Fact]
    public async Task A_page_with_comments_is_deleted_for_good()
    {
        await AuthAsync("comments5");
        var page = await CreatePageAsync(new { id = "b1", type = "paragraph", content = new[] { new { text = "Texto" } } });
        var thread = await SendAsync<CommentItem>(HttpMethod.Post, $"{PagesUrl}/{page}/comments", new { blockId = "b1", body = "Um" });
        await SendAsync<CommentItem>(HttpMethod.Post, $"{CommentsUrl}/{thread.Id}/replies", new { body = "Dois" });

        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync($"{PagesUrl}/{page}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync("/api/v1/notes/trash")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{PagesUrl}/{page}/comments")).StatusCode);
    }

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private async Task<Guid> CreatePageAsync(params object[] blocks)
        => (await SendAsync<PageItem>(HttpMethod.Post, PagesUrl, new { title = "Página", content = NoteBlocks.Doc(blocks) })).Id;

    private async Task<List<CommentItem>> ListAsync(Guid page)
        => (await _client.GetFromJsonAsync<Envelope<List<CommentItem>>>($"{PagesUrl}/{page}/comments"))!.Data;

    private async Task<List<StampItem>> StampsAsync(Guid page)
        => (await _client.GetFromJsonAsync<Envelope<List<StampItem>>>($"{PagesUrl}/{page}/block-stamps"))!.Data;

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object body)
    {
        var response = await _client.SendAsync(new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<T>>())!.Data;
    }

    private sealed record PageItem(Guid Id);
    private sealed record CommentItem(Guid Id, string BlockId, Guid? ThreadId, string? Quote, string Body, DateTimeOffset? ResolvedAt, DateTimeOffset? EditedAt);
    private sealed record StampItem(string BlockId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string UpdatedSource);
    private sealed record Envelope<T>(T Data);
}
