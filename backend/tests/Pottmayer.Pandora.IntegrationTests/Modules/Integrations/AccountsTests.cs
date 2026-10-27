using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Integrations;

/// <summary>
/// Several accounts per provider (phase I5): password accounts keyed by username, renaming, replacing a
/// secret, the ownership checks of the account endpoints and of the credential port, and the "used by"
/// list collected from the consumer modules.
/// </summary>
[Collection("Integration")]
public sealed class AccountsTests : IAsyncLifetime
{
    private const string TitanPasswordUrl = "/api/v1/integrations/titan/password";

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
    public async Task Passwords_make_one_account_per_username_and_the_same_username_replaces_its_password()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "alice@example.com", "alice");

        var first = await SavePasswordAsync("me@one.com", "pw-1", label: "One");
        var second = await SavePasswordAsync("me@two.com", "pw-2");
        var again = await SavePasswordAsync("me@one.com", "pw-1-new", label: "ignored on update");

        Assert.NotEqual(first, second);
        Assert.Equal(first, again);

        var accounts = await AccountsApi.AccountsAsync(_client);
        Assert.Equal(2, accounts.Count);
        Assert.Equal("One", accounts.Single(a => a.Id == first).Label);
        Assert.Equal("me@two.com", accounts.Single(a => a.Id == second).Label);
        Assert.DoesNotContain("pw-1", await (await _client.GetAsync(AccountsApi.AccountsUrl)).Content.ReadAsStringAsync());

        var titan = Assert.Single(await AccountsApi.ProvidersAsync(_client), p => p.Provider == "titan");
        Assert.Equal("password", titan.AuthKind);
        Assert.Equal(2, titan.AccountCount);

        var alice = await AccountsApi.UserIdAsync(_factory.ConnectionString, "alice@example.com");
        var credential = await CredentialsAsync(p => p.GetPasswordAsync(alice, first));
        Assert.True(credential.IsSuccess);
        Assert.Equal(new PasswordCredential("me@one.com", "pw-1-new"), credential.Value);
    }

    [Theory]
    [InlineData("", "pw")]
    [InlineData("me@one.com", "")]
    public async Task Password_save_rejects_a_missing_username_or_password(string username, string password)
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "bob@example.com", "bob");

        var response = await _client.PutAsJsonAsync(TitanPasswordUrl, new { username, password });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Rename_changes_the_label_and_refuses_a_blank_one()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "carol@example.com", "carol");
        var id = await AccountsApi.SaveApiKeyAsync(_client, "sk-key-1111");

        var rename = await _client.PatchAsJsonAsync($"{AccountsApi.AccountsUrl}/{id}", new { label = "  Personal " });
        var blank = await _client.PatchAsJsonAsync($"{AccountsApi.AccountsUrl}/{id}", new { label = " " });

        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blank.StatusCode);
        Assert.Equal("Personal", Assert.Single(await AccountsApi.AccountsAsync(_client)).Label);
    }

    [Fact]
    public async Task Replace_secret_swaps_the_key_and_its_hint()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "dave@example.com", "dave");
        var id = await AccountsApi.SaveApiKeyAsync(_client, "sk-old-1111", label: "Work");

        var response = await _client.PutAsJsonAsync($"{AccountsApi.AccountsUrl}/{id}/secret", new { secret = "sk-new-2222" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var account = Assert.Single(await AccountsApi.AccountsAsync(_client));
        Assert.Equal("••••2222", account.DisplayName);
        Assert.Equal("Work", account.Label);

        var userId = await AccountsApi.UserIdAsync(_factory.ConnectionString, "dave@example.com");
        var key = await CredentialsAsync(p => p.GetApiKeyAsync(userId, id));
        Assert.Equal("sk-new-2222", key.Value);
    }

    [Fact]
    public async Task Another_users_account_cannot_be_touched_or_read()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "erin@example.com", "erin");
        var erinsKey = await AccountsApi.SaveApiKeyAsync(_client, "sk-erin-1111");

        var other = _factory.CreateClient();
        await IdentityHelper.AuthenticateAsync(other, _factory.ConnectionString, "frank@example.com", "frank");

        Assert.Equal(HttpStatusCode.NotFound,
            (await other.PatchAsJsonAsync($"{AccountsApi.AccountsUrl}/{erinsKey}", new { label = "mine" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.PutAsJsonAsync($"{AccountsApi.AccountsUrl}/{erinsKey}/secret", new { secret = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.DeleteAsync($"{AccountsApi.AccountsUrl}/{erinsKey}")).StatusCode);
        Assert.Empty(await AccountsApi.AccountsAsync(other));

        // The port gives the same answer as for an account that does not exist.
        var frank = await AccountsApi.UserIdAsync(_factory.ConnectionString, "frank@example.com");
        var stolen = await CredentialsAsync(p => p.GetApiKeyAsync(frank, erinsKey));
        var missing = await CredentialsAsync(p => p.GetApiKeyAsync(frank, Guid.NewGuid()));
        Assert.Equal("Integrations.NotConnected", stolen.Errors[0].Code);
        Assert.Equal(missing.Errors[0].Code, stolen.Errors[0].Code);
    }

    [Fact]
    public async Task Accounts_list_where_the_assistant_uses_them()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "gina@example.com", "gina");
        var used = await AccountsApi.SaveApiKeyAsync(_client, "sk-used-1111");
        var unused = await AccountsApi.SaveApiKeyAsync(_client, "sk-unused-2222");

        var save = await _client.PostAsJsonAsync("/api/v1/assistant/profile", new
        {
            provider = "gemini",
            chatAccountId = used,
            model = "gemini-test",
            isEnabled = false,
            localeOverride = (string?)null,
            confirmationLevel = "balanced",
        });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var accounts = await AccountsApi.AccountsAsync(_client);
        var usage = Assert.Single(accounts.Single(a => a.Id == used).Usages);
        Assert.Equal(new AccountsApi.UsageItem("assistant", "chat", null), usage);
        Assert.Empty(accounts.Single(a => a.Id == unused).Usages);
    }

    [Fact]
    public async Task A_failing_usage_source_is_skipped()
    {
        var factory = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddScoped<IExternalAccountUsageSource, ThrowingUsageSource>()));
        var client = factory.CreateClient();
        await IdentityHelper.AuthenticateAsync(client, _factory.ConnectionString, "hank@example.com", "hank");
        await AccountsApi.SaveApiKeyAsync(client, "sk-key-1111");

        var account = Assert.Single(await AccountsApi.AccountsAsync(client));

        Assert.Empty(account.Usages);
    }

    private async Task<Guid> SavePasswordAsync(string username, string password, string? label = null) =>
        await AccountsApi.ReadDataAsync<Guid>(
            await _client.PutAsJsonAsync(TitanPasswordUrl, new { username, password, label }));

    private async Task<T> CredentialsAsync<T>(Func<IExternalCredentialProvider, Task<T>> call)
    {
        using var scope = _factory.Services.CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<IExternalCredentialProvider>());
    }

    private sealed class ThrowingUsageSource : IExternalAccountUsageSource
    {
        public Task<IReadOnlyList<ExternalAccountUsage>> ListAsync(Guid userId, CancellationToken ct = default) =>
            throw new InvalidOperationException("consumer is down");
    }
}
