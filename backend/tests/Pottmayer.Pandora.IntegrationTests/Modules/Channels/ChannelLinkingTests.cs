using System.Net;
using System.Net.Http.Json;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Channels;

/// <summary>
/// The linking surface against a real database: issuing a deep link, listing addresses, unlinking,
/// and the test send. Exercises the chn001 / chn002 mappings, including the value-object columns.
/// </summary>
[Collection("Integration")]
public sealed class ChannelLinkingTests : IAsyncLifetime
{
    private const string ChannelsUrl = "/api/v1/channels";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ChannelLinkingTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record SingleEnvelope<T>(T Data);
    private sealed record LinkResponse(string Url, DateTimeOffset ExpiresAt);
    private sealed record ChannelResponse(
        string Channel, string Address, bool IsVerified, bool IsEnabled, string? DisabledReason, DateTimeOffset? VerifiedAt);

    [Fact]
    public async Task Link_issues_a_deep_link_and_stores_only_the_hash()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "link@example.com", "linkuser");

        var response = await _client.PostAsync($"{ChannelsUrl}/telegram/link", content: null);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var link = (await response.Content.ReadFromJsonAsync<SingleEnvelope<LinkResponse>>())!.Data;
        Assert.StartsWith("https://t.me/", link.Url);

        var code = link.Url.Split("?start=")[1];
        var stored = await ScalarAsync("SELECT token FROM channels.chn002_channel_link_token LIMIT 1");
        Assert.NotEqual(code, stored);
        Assert.Equal(64, ((string)stored!).Length);
    }

    [Fact]
    public async Task Link_is_refused_for_a_channel_with_no_handshake()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "noshake@example.com", "noshake");

        var response = await _client.PostAsync($"{ChannelsUrl}/email/link", content: null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Listing_returns_the_linked_address()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "list@example.com", "listuser");
        var userId = await UserIdAsync("list@example.com");
        await InsertTelegramLinkAsync(userId, "555000111");

        var channels = await _client.GetFromJsonAsync<SingleEnvelope<List<ChannelResponse>>>(ChannelsUrl);

        var telegram = Assert.Single(channels!.Data);
        Assert.Equal("telegram", telegram.Channel);
        Assert.Equal("555000111", telegram.Address);
        Assert.True(telegram.IsVerified);
        Assert.True(telegram.IsEnabled);
    }

    [Fact]
    public async Task Unlink_removes_the_row()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "unlink@example.com", "unlinkuser");
        var userId = await UserIdAsync("unlink@example.com");
        await InsertTelegramLinkAsync(userId, "555000222");

        var response = await _client.DeleteAsync($"{ChannelsUrl}/telegram/link");

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        Assert.Equal(0L, await ScalarAsync("SELECT count(*) FROM channels.chn001_user_channel"));
    }

    [Fact]
    public async Task Unlink_without_a_link_is_a_not_found()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "nolink@example.com", "nolinkuser");

        var response = await _client.DeleteAsync($"{ChannelsUrl}/telegram/link");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Test_send_queues_a_notification_for_the_linked_address()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "test@example.com", "testuser");
        var userId = await UserIdAsync("test@example.com");
        await InsertTelegramLinkAsync(userId, "555000333");

        var response = await _client.PostAsync($"{ChannelsUrl}/telegram/test", content: null);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        var recipient = await ScalarAsync(
            "SELECT recipient FROM channels.chn006_notification WHERE channel = 'telegram' LIMIT 1");
        Assert.Equal("555000333", recipient);
    }

    [Fact]
    public async Task Test_send_is_refused_when_the_address_is_disabled()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "off@example.com", "offuser");
        var userId = await UserIdAsync("off@example.com");
        await InsertTelegramLinkAsync(userId, "555000444", isEnabled: false);

        var response = await _client.PostAsync($"{ChannelsUrl}/telegram/test", content: null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task The_endpoints_require_authentication()
    {
        var anonymous = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(ChannelsUrl)).StatusCode);
    }

    // ── helpers ──

    private async Task<Guid> UserIdAsync(string email) =>
        (Guid)(await ScalarAsync("SELECT id FROM identity.idt001_user WHERE email = $1", email))!;

    private async Task InsertTelegramLinkAsync(Guid userId, string chatId, bool isEnabled = true)
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO channels.chn001_user_channel
                (user_id, channel, address, locale, is_verified, verified_at, is_enabled)
            VALUES ($1, 'telegram', $2, 'pt-BR', true, now(), $3)
            """;
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(chatId);
        cmd.Parameters.AddWithValue(isEnabled);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<object?> ScalarAsync(string sql, params object[] parameters)
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var p in parameters)
            cmd.Parameters.AddWithValue(p);
        return await cmd.ExecuteScalarAsync();
    }
}
