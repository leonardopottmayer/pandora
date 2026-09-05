using System.Net;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Integrations;

/// <summary>
/// Covers the api-key write path (fase I3): storing a provider key, seeing it reflected in the provider
/// catalog and account list (masked, never in plaintext), replacing it, and the validation/auth guards.
/// </summary>
[Collection("Integration")]
public sealed class ApiKeyTests : IAsyncLifetime
{
    private const string GeminiApiKeyUrl = "/api/v1/integrations/gemini/api-key";
    private const string ProvidersUrl = "/api/v1/integrations/providers";
    private const string AccountsUrl = "/api/v1/integrations/accounts";

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
    public async Task Save_stores_the_key_and_marks_the_provider_connected()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "alice@example.com", "alice");
        const string secret = "sk-secret-ABCD";

        var save = await _client.PutAsJsonAsync(GeminiApiKeyUrl, new { apiKey = secret });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        // Catalog reflects it as a connected api-key provider.
        var providersBody = await (await _client.GetAsync(ProvidersUrl)).Content.ReadAsStringAsync();
        Assert.DoesNotContain(secret, providersBody);

        var providers = await GetAsync<List<ProviderItem>>(ProvidersUrl);
        var gemini = Assert.Single(providers, p => p.Provider == "gemini");
        Assert.Equal("api-key", gemini.AuthKind);
        Assert.True(gemini.Connected);
        Assert.Equal("connected", gemini.Status);

        // Account list shows the masked hint, never the raw key.
        var accountsBody = await (await _client.GetAsync(AccountsUrl)).Content.ReadAsStringAsync();
        Assert.DoesNotContain(secret, accountsBody);

        var accounts = await GetAsync<List<AccountItem>>(AccountsUrl);
        var account = Assert.Single(accounts, a => a.Provider == "gemini");
        Assert.Equal("api-key", account.AuthKind);
        Assert.Equal("connected", account.Status);
        Assert.EndsWith("ABCD", account.DisplayName);
        Assert.NotEqual(secret, account.DisplayName);
    }

    [Fact]
    public async Task Save_twice_replaces_the_key_without_adding_an_account()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "bob@example.com", "bob");

        await _client.PutAsJsonAsync(GeminiApiKeyUrl, new { apiKey = "sk-first-1111" });
        await _client.PutAsJsonAsync(GeminiApiKeyUrl, new { apiKey = "sk-second-2222" });

        var accounts = await GetAsync<List<AccountItem>>(AccountsUrl);
        var account = Assert.Single(accounts, a => a.Provider == "gemini");
        Assert.EndsWith("2222", account.DisplayName);
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
    public async Task Save_rejects_an_empty_key()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "dave@example.com", "dave");

        var response = await _client.PutAsJsonAsync(GeminiApiKeyUrl, new { apiKey = "   " });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private async Task<T> GetAsync<T>(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>();
        return envelope!.Data;
    }

    private sealed record Envelope<T>(T Data);

    private sealed record ProviderItem(string Provider, string AuthKind, bool Connected, string? Status);

    private sealed record AccountItem(string Provider, string AuthKind, string? DisplayName, string Status);
}
