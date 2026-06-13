using System.Net;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Finances;

/// <summary>
/// Covers the phase-07 tags: CRUD with unique-name, polymorphic links with target validation
/// (404 on foreign/unknown), the unique trio, the OR filter on the statement with paging, the
/// PUT-tags shortcut that replaces a set, and the audited cascade on delete. Maps to the four
/// acceptance criteria of roadmap/07-tags.md.
/// </summary>
[Collection("Integration")]
public sealed class TagsTests : IAsyncLifetime
{
    private const string Accounts = "/api/v1/finances/accounts";
    private const string Transactions = "/api/v1/finances/transactions";
    private const string Tags = "/api/v1/finances/tags";
    private const string AuditUrl = "/api/v1/finances/audit";

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

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
    public async Task Create_lists_and_rejects_duplicate_name()
    {
        await AuthAsync("tag-crud");

        var travel = await CreateTagAsync(new { name = "Travel", color = "#0af" });
        Assert.Equal("Travel", travel.Name);

        var dup = await _client.PostAsJsonAsync(Tags, new { name = "Travel" });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        var all = await ListTagsAsync();
        Assert.Single(all, t => t.Id == travel.Id);
    }

    [Fact]
    public async Task Linking_to_a_foreign_or_unknown_entity_is_404()
    {
        await AuthAsync("tag-404");
        var tag = await CreateTagAsync(new { name = "X" });

        // Unknown transaction id.
        var unknown = await _client.PostAsJsonAsync($"{Tags}/{tag.Id}/links",
            new { entityType = "transaction", entityId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        // A real account, but owned by a different user.
        var account = await CreateAccountAsync(new { name = "Mine", type = "cash", currency = "BRL", displayOrder = 0 });
        await AuthAsync("tag-404-other");
        var otherTag = await CreateTagAsync(new { name = "Y" });
        var foreign = await _client.PostAsJsonAsync($"{Tags}/{otherTag.Id}/links",
            new { entityType = "account", entityId = account });
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task Linking_the_same_trio_twice_does_not_duplicate()
    {
        await AuthAsync("tag-dup-link");
        var tag = await CreateTagAsync(new { name = "Home" });
        var account = await CreateAccountAsync(new { name = "Wallet", type = "cash", currency = "BRL", displayOrder = 0 });

        var first = await _client.PostAsJsonAsync($"{Tags}/{tag.Id}/links", new { entityType = "account", entityId = account });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var second = await _client.PostAsJsonAsync($"{Tags}/{tag.Id}/links", new { entityType = "account", entityId = account });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var links = (await _client.GetFromJsonAsync<LinkListEnvelope>($"{Tags}/{tag.Id}/links"))!.Data;
        Assert.Single(links);
    }

    [Fact]
    public async Task Statement_filters_by_multiple_tags_with_or_semantics_and_paging()
    {
        await AuthAsync("tag-filter");
        var account = await CreateAccountAsync(new { name = "Wallet", type = "cash", currency = "BRL", displayOrder = 0 });
        var work = await CreateTagAsync(new { name = "Work" });
        var fun = await CreateTagAsync(new { name = "Fun" });

        var t1 = await CreateTxAsync(new { accountId = account, kind = "expense", amount = 10m, occurredOn = Today.AddDays(-2), description = "A" });
        var t2 = await CreateTxAsync(new { accountId = account, kind = "expense", amount = 20m, occurredOn = Today.AddDays(-1), description = "B" });
        await CreateTxAsync(new { accountId = account, kind = "expense", amount = 30m, occurredOn = Today, description = "C" }); // untagged

        await SetTransactionTagsAsync(t1, work.Id);
        await SetTransactionTagsAsync(t2, fun.Id);

        // OR: both tagged transactions match.
        var both = await ListTxAsync($"?accountId={account}&tags={work.Id}&tags={fun.Id}");
        Assert.Equal(2, both.Count);
        Assert.DoesNotContain(both, t => t.Description == "C");

        // Single tag narrows to one.
        var onlyWork = await ListTxAsync($"?accountId={account}&tags={work.Id}");
        Assert.Single(onlyWork);
        Assert.Equal("A", onlyWork[0].Description);

        // Paging stays intact over the filtered set.
        var page1 = await ListTxAsync($"?accountId={account}&tags={work.Id}&tags={fun.Id}&skip=0&take=1");
        var page2 = await ListTxAsync($"?accountId={account}&tags={work.Id}&tags={fun.Id}&skip=1&take=1");
        Assert.Single(page1);
        Assert.Single(page2);
        Assert.NotEqual(page1[0].Id, page2[0].Id);
    }

    [Fact]
    public async Task Put_tags_replaces_the_whole_set()
    {
        await AuthAsync("tag-replace");
        var account = await CreateAccountAsync(new { name = "Wallet", type = "cash", currency = "BRL", displayOrder = 0 });
        var tx = await CreateTxAsync(new { accountId = account, kind = "expense", amount = 10m, occurredOn = Today, description = "A" });
        var a = await CreateTagAsync(new { name = "A" });
        var b = await CreateTagAsync(new { name = "B" });
        var c = await CreateTagAsync(new { name = "C" });

        var first = await SetTransactionTagsAsync(tx, a.Id, b.Id);
        Assert.Equal(2, first.Count);

        // Replacing with {b, c} drops a and adds c.
        var replaced = await SetTransactionTagsAsync(tx, b.Id, c.Id);
        var names = replaced.Select(t => t.Name).OrderBy(n => n).ToList();
        Assert.Equal(["B", "C"], names);

        var byA = await ListTxAsync($"?accountId={account}&tags={a.Id}");
        Assert.Empty(byA);
    }

    [Fact]
    public async Task Deleting_a_tag_clears_links_and_audits()
    {
        await AuthAsync("tag-delete");
        var account = await CreateAccountAsync(new { name = "Wallet", type = "cash", currency = "BRL", displayOrder = 0 });
        var tx = await CreateTxAsync(new { accountId = account, kind = "expense", amount = 10m, occurredOn = Today, description = "A" });
        var tag = await CreateTagAsync(new { name = "Doomed" });

        await SetTransactionTagsAsync(tx, tag.Id);

        var delete = await _client.DeleteAsync($"{Tags}/{tag.Id}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        // The link is gone: the transaction no longer matches a filter by that tag.
        Assert.Empty(await ListTxAsync($"?accountId={account}&tags={tag.Id}"));

        var audit = await _client.GetFromJsonAsync<AuditEnvelope>($"{AuditUrl}?entityType=tag&entityId={tag.Id}");
        var types = audit!.Data.Select(e => e.EventType).ToList();
        Assert.Contains("tag.created", types);
        Assert.Contains("tag.linked", types);
        Assert.Contains("tag.deleted", types);
    }

    // ── helpers ──

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private async Task<TagNode> CreateTagAsync(object body)
    {
        var response = await _client.PostAsJsonAsync(Tags, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TagEnvelope>())!.Data;
    }

    private async Task<IReadOnlyList<TagNode>> ListTagsAsync() =>
        (await _client.GetFromJsonAsync<TagListEnvelope>(Tags))!.Data;

    private async Task<IReadOnlyList<TagNode>> SetTransactionTagsAsync(Guid txId, params Guid[] tagIds)
    {
        var response = await _client.PutAsJsonAsync($"{Transactions}/{txId}/tags", new { tagIds });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TagListEnvelope>())!.Data;
    }

    private async Task<Guid> CreateAccountAsync(object body)
    {
        var response = await _client.PostAsJsonAsync(Accounts, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccountEnvelope>())!.Data.Id;
    }

    private async Task<Guid> CreateTxAsync(object body)
    {
        var response = await _client.PostAsJsonAsync(Transactions, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TxEnvelope>())!.Data.Id;
    }

    private async Task<IReadOnlyList<TxNode>> ListTxAsync(string qs) =>
        (await _client.GetFromJsonAsync<TxListEnvelope>($"{Transactions}{qs}"))!.Data;

    private sealed record TagEnvelope(TagNode Data);
    private sealed record TagListEnvelope(IReadOnlyList<TagNode> Data);
    private sealed record TagNode(Guid Id, string Name, string? Color);

    private sealed record LinkListEnvelope(IReadOnlyList<LinkNode> Data);
    private sealed record LinkNode(Guid Id, Guid TagId, string EntityType, Guid EntityId);

    private sealed record AccountEnvelope(AccountNode Data);
    private sealed record AccountNode(Guid Id);

    private sealed record TxEnvelope(TxNode Data);
    private sealed record TxListEnvelope(IReadOnlyList<TxNode> Data);
    private sealed record TxNode(Guid Id, string Description);

    private sealed record AuditEnvelope(IReadOnlyList<AuditNode> Data);
    private sealed record AuditNode(string EventType);
}
