using System.Net;
using System.Net.Http.Json;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Identity;

/// <summary>
/// Covers the authenticated user-preferences endpoints: reading before anything is set, upserting a
/// theme and language, validation of unsupported values, and the auth requirement.
/// </summary>
[Collection("Integration")]
public sealed class PreferencesTests : IAsyncLifetime
{
    private const string PreferencesUrl = "/api/v1/identity/preferences";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PreferencesTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Get_returns_unauthorized_without_a_token()
    {
        var response = await _client.GetAsync(PreferencesUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_returns_not_found_before_any_preferences_are_set()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "alice@example.com", "alice");

        var response = await _client.GetAsync(PreferencesUrl);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upsert_then_get_returns_the_saved_theme_and_language()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "bob@example.com", "bob");

        var upsert = await _client.PutAsJsonAsync(PreferencesUrl, new { theme = "dark", language = "en" });
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);

        var get = await _client.GetAsync(PreferencesUrl);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var prefs = await ReadPreferencesAsync(get);
        Assert.Equal("dark", prefs.Theme);
        Assert.Equal("en", prefs.Language);
    }

    [Fact]
    public async Task Upsert_overwrites_the_previous_values()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "carol@example.com", "carol");

        await _client.PutAsJsonAsync(PreferencesUrl, new { theme = "dark", language = "en" });
        await _client.PutAsJsonAsync(PreferencesUrl, new { theme = "light", language = "pt-BR" });

        var prefs = await ReadPreferencesAsync(await _client.GetAsync(PreferencesUrl));
        Assert.Equal("light", prefs.Theme);
        Assert.Equal("pt-BR", prefs.Language);
    }

    [Fact]
    public async Task Upsert_rejects_an_unsupported_theme()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "dave@example.com", "dave");

        var response = await _client.PutAsJsonAsync(PreferencesUrl, new { theme = "neon", language = "en" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Upsert_rejects_an_unsupported_language()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "erin@example.com", "erin");

        var response = await _client.PutAsJsonAsync(PreferencesUrl, new { theme = "dark", language = "fr" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private static async Task<PreferencesData> ReadPreferencesAsync(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<Envelope>();
        return envelope!.Data;
    }

    private sealed record Envelope(PreferencesData Data);
    private sealed record PreferencesData(string Theme, string Language);
}
