using System.Net;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Integrations;

/// <summary>
/// Covers the api-key write path: every save is a new account (a user can hold several keys per
/// provider), the key never comes back in plaintext, the catalog lists each provider once, and the
/// validation/auth guards.
/// </summary>
[Collection("Integration")]
public sealed class ApiKeyTests : IAsyncLifetime
{
    private const string GeminiApiKeyUrl = "/api/v1/integrations/gemini/api-key";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApiKeyTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Save_requires_authentication()
    {
        var response = await _client.PutAsJsonAsync(GeminiApiKeyUrl, new { apiKey = "sk-secret-ABCD" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Save_creates_an_account_and_never_echoes_the_key()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "alice@example.com", "alice");
        const string secret = "sk-secret-ABCD";

        var save = await _client.PutAsJsonAsync(GeminiApiKeyUrl, new { apiKey = secret });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var accountId = await AccountsApi.ReadDataAsync<Guid>(save);

        Assert.DoesNotContain(secret, await (await _client.GetAsync(AccountsApi.ProvidersUrl)).Content.ReadAsStringAsync());
        Assert.DoesNotContain(secret, await (await _client.GetAsync(AccountsApi.AccountsUrl)).Content.ReadAsStringAsync());

        var gemini = Assert.Single(await AccountsApi.ProvidersAsync(_client), p => p.Provider == "gemini");
        Assert.Equal("api-key", gemini.AuthKind);
        Assert.Equal(1, gemini.AccountCount);

        var account = Assert.Single(await AccountsApi.AccountsAsync(_client));
        Assert.Equal(accountId, account.Id);
        Assert.Equal("api-key", account.AuthKind);
        Assert.Equal("connected", account.Status);
        Assert.Equal("••••ABCD", account.DisplayName);
        Assert.Equal("Google Gemini ••••ABCD", account.Label);
    }

    [Fact]
    public async Task Saving_twice_adds_a_second_account_and_the_catalog_lists_the_provider_once()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "bob@example.com", "bob");

        await _client.PutAsJsonAsync(GeminiApiKeyUrl, new { apiKey = "sk-first-1111", label = "Personal" });
        await _client.PutAsJsonAsync(GeminiApiKeyUrl, new { apiKey = "sk-second-2222", label = "Work" });

        var accounts = await AccountsApi.AccountsAsync(_client);
        Assert.Equal(["Personal", "Work"], accounts.Select(a => a.Label).Order());

        var gemini = Assert.Single(await AccountsApi.ProvidersAsync(_client), p => p.Provider == "gemini");
        Assert.Equal(2, gemini.AccountCount);
    }

    [Fact]
    public async Task Save_rejects_an_unknown_provider()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "carol@example.com", "carol");

        var response = await _client.PutAsJsonAsync(
            "/api/v1/integrations/no-such-provider/api-key", new { apiKey = "sk-secret-ABCD" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Save_rejects_a_password_provider()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "erin@example.com", "erin");

        var response = await _client.PutAsJsonAsync("/api/v1/integrations/titan/api-key", new { apiKey = "sk-secret-ABCD" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Save_rejects_an_empty_key()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "dave@example.com", "dave");

        var response = await _client.PutAsJsonAsync(GeminiApiKeyUrl, new { apiKey = "   " });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
