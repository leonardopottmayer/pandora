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
    public async Task Upsert_then_get_returns_the_saved_preferences()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "bob@example.com", "bob");

        var upsert = await _client.PutAsJsonAsync(PreferencesUrl, Body(
            theme: "dark", language: "en",
            timeZone: "America/Sao_Paulo", weekStartsOn: "monday", defaultAlertOffsetMinutes: -30));
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);

        var get = await _client.GetAsync(PreferencesUrl);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var prefs = await ReadPreferencesAsync(get);
        Assert.Equal("dark", prefs.Theme);
        Assert.Equal("en", prefs.Language);
        Assert.Equal("America/Sao_Paulo", prefs.TimeZone);
        Assert.Equal("monday", prefs.WeekStartsOn);
        Assert.Equal(-30, prefs.DefaultAlertOffsetMinutes);
    }

    [Fact]
    public async Task Upsert_overwrites_the_previous_values()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "carol@example.com", "carol");

        await _client.PutAsJsonAsync(PreferencesUrl, Body(
            theme: "dark", language: "en",
            timeZone: "America/Sao_Paulo", weekStartsOn: "sunday", defaultAlertOffsetMinutes: -15));
        await _client.PutAsJsonAsync(PreferencesUrl, Body(
            theme: "light", language: "pt-BR",
            timeZone: "UTC", weekStartsOn: "monday", defaultAlertOffsetMinutes: 0));

        var prefs = await ReadPreferencesAsync(await _client.GetAsync(PreferencesUrl));
        Assert.Equal("light", prefs.Theme);
        Assert.Equal("pt-BR", prefs.Language);
        Assert.Equal("UTC", prefs.TimeZone);
        Assert.Equal("monday", prefs.WeekStartsOn);
        Assert.Equal(0, prefs.DefaultAlertOffsetMinutes);
    }

    [Fact]
    public async Task Upsert_rejects_an_unsupported_theme()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "dave@example.com", "dave");

        var response = await _client.PutAsJsonAsync(PreferencesUrl, Body(theme: "neon", language: "en"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Upsert_rejects_an_unsupported_language()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "erin@example.com", "erin");

        var response = await _client.PutAsJsonAsync(PreferencesUrl, Body(theme: "dark", language: "fr"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Upsert_rejects_an_invalid_time_zone()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "frank@example.com", "frank");

        var response = await _client.PutAsJsonAsync(PreferencesUrl, Body(
            theme: "dark", language: "en", timeZone: "Mars/Olympus_Mons"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Upsert_rejects_an_invalid_week_start()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "grace@example.com", "grace");

        var response = await _client.PutAsJsonAsync(PreferencesUrl, Body(
            theme: "dark", language: "en", weekStartsOn: "someday"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private static object Body(
        string theme, string language,
        string timeZone = "America/Sao_Paulo", string weekStartsOn = "sunday",
        int defaultAlertOffsetMinutes = -15)
        => new { theme, language, timeZone, weekStartsOn, defaultAlertOffsetMinutes };

    private static async Task<PreferencesData> ReadPreferencesAsync(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<Envelope>();
        return envelope!.Data;
    }

    private sealed record Envelope(PreferencesData Data);
    private sealed record PreferencesData(
        string Theme, string Language, string TimeZone, string WeekStartsOn, int DefaultAlertOffsetMinutes);
}
