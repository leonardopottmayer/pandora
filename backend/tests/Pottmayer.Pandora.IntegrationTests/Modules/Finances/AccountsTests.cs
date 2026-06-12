using System.Net;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Finances;

/// <summary>
/// Covers the account endpoints: CRUD with validation (type, currency, name uniqueness), the
/// archive/unarchive lifecycle, owner scoping (404 on foreign resources), and the audit trail.
/// </summary>
[Collection("Integration")]
public sealed class AccountsTests : IAsyncLifetime
{
    private const string Url = "/api/v1/finances/accounts";
    private const string AuditUrl = "/api/v1/finances/audit";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AccountsTests(PandoraWebApplicationFactory factory)
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
        await AuthAsync("acc1");

        var created = await CreateAsync(new { name = "Wallet", type = "cash", currency = "brl", displayOrder = 1 });
        Assert.Equal(HttpStatusCode.OK, created.status);
        Assert.Equal("BRL", created.dto!.Currency); // normalized upper-case

        var get = await _client.GetFromJsonAsync<SingleEnvelope>($"{Url}/{created.dto.Id}");
        Assert.Equal("Wallet", get!.Data.Name);

        var list = await ListAsync();
        Assert.Single(list, a => a.Id == created.dto.Id);
    }

    [Fact]
    public async Task Rejects_invalid_type_and_currency()
    {
        await AuthAsync("acc2");

        var badType = await CreateAsync(new { name = "A", type = "debit", currency = "BRL", displayOrder = 0 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badType.status);

        var badCurrency = await CreateAsync(new { name = "B", type = "cash", currency = "R$", displayOrder = 0 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badCurrency.status);
    }

    [Fact]
    public async Task Rejects_duplicate_name()
    {
        await AuthAsync("acc3");

        Assert.Equal(HttpStatusCode.OK, (await CreateAsync(new { name = "Main", type = "checking", currency = "USD", displayOrder = 0 })).status);
        var dup = await CreateAsync(new { name = "Main", type = "savings", currency = "USD", displayOrder = 0 });
        Assert.Equal(HttpStatusCode.Conflict, dup.status);
    }

    [Fact]
    public async Task Update_changes_mutable_fields()
    {
        await AuthAsync("acc4");
        var acc = await CreateAsync(new { name = "Old", type = "checking", currency = "BRL", displayOrder = 0 });

        var response = await _client.PutAsJsonAsync($"{Url}/{acc.dto!.Id}",
            new { name = "New", type = "savings", institution = "Bank", displayOrder = 3 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = (await response.Content.ReadFromJsonAsync<SingleEnvelope>())!.Data;
        Assert.Equal("New", updated.Name);
        Assert.Equal("savings", updated.Type);
        Assert.Equal("BRL", updated.Currency); // currency immutable
    }

    [Fact]
    public async Task Archive_hides_from_default_list_blocks_edit_and_reactivates()
    {
        await AuthAsync("acc5");
        var acc = await CreateAsync(new { name = "Temp", type = "cash", currency = "BRL", displayOrder = 0 });
        var id = acc.dto!.Id;

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Url}/{id}/archive", null)).StatusCode);

        Assert.DoesNotContain(await ListAsync(), a => a.Id == id);
        Assert.Contains(await ListAsync("?includeArchived=true"), a => a.Id == id);

        // Editing an archived account is blocked.
        var edit = await _client.PutAsJsonAsync($"{Url}/{id}", new { name = "X", type = "cash", displayOrder = 0 });
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"{Url}/{id}/unarchive", null)).StatusCode);
        Assert.Contains(await ListAsync(), a => a.Id == id);
    }

    [Fact]
    public async Task Delete_removes_the_account()
    {
        await AuthAsync("acc6");
        var acc = await CreateAsync(new { name = "Disposable", type = "cash", currency = "BRL", displayOrder = 0 });

        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync($"{Url}/{acc.dto!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{Url}/{acc.dto.Id}")).StatusCode);
    }

    [Fact]
    public async Task Foreign_account_returns_not_found()
    {
        await AuthAsync("owner-acc");
        var acc = await CreateAsync(new { name = "Private", type = "cash", currency = "BRL", displayOrder = 0 });
        var id = acc.dto!.Id;

        await AuthAsync("intruder-acc"); // re-auth as a different user on the same client
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{Url}/{id}")).StatusCode);
    }

    [Fact]
    public async Task Mutations_are_audited()
    {
        await AuthAsync("acc7");
        var acc = await CreateAsync(new { name = "Audited", type = "cash", currency = "BRL", displayOrder = 0 });
        var id = acc.dto!.Id;
        await _client.PutAsJsonAsync($"{Url}/{id}", new { name = "Audited2", type = "cash", displayOrder = 1 });
        await _client.PostAsync($"{Url}/{id}/archive", null);

        var audit = await _client.GetFromJsonAsync<AuditEnvelope>($"{AuditUrl}?entityType=account&entityId={id}");
        var types = audit!.Data.Select(e => e.EventType).ToList();
        Assert.Contains("account.created", types);
        Assert.Contains("account.updated", types);
        Assert.Contains("account.archived", types);
    }

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private async Task<IReadOnlyList<AccountNode>> ListAsync(string qs = "")
        => (await _client.GetFromJsonAsync<ListEnvelope>($"{Url}{qs}"))!.Data;

    private async Task<(HttpStatusCode status, AccountNode? dto)> CreateAsync(object body)
    {
        var response = await _client.PostAsJsonAsync(Url, body);
        if (response.StatusCode != HttpStatusCode.OK)
            return (response.StatusCode, null);
        return (response.StatusCode, (await response.Content.ReadFromJsonAsync<SingleEnvelope>())!.Data);
    }

    private sealed record ListEnvelope(IReadOnlyList<AccountNode> Data);
    private sealed record SingleEnvelope(AccountNode Data);
    private sealed record AccountNode(
        Guid Id, string Name, string Type, string Currency, string? Institution,
        string? Description, string? Color, string? Icon, int DisplayOrder, DateTimeOffset? ArchivedAt);

    private sealed record AuditEnvelope(IReadOnlyList<AuditNode> Data);
    private sealed record AuditNode(string EntityType, Guid EntityId, string EventType);
}
