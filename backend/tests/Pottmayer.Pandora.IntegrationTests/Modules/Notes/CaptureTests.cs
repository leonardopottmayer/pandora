using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Assistant.Application.Subscribers;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Pandora.Modules.Notes.Abstractions.Capture;
using Pottmayer.Tars.Ai.Chat.Abstractions;
using Pottmayer.Tars.Messaging.Abstractions;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

/// <summary>The direct Telegram capture path, including durable event receipts and the normal Notes save data.</summary>
[Collection("Integration")]
public sealed class CaptureTests : IAsyncLifetime
{
    private const string Pages = "/api/v1/notes/pages";
    private static readonly DateTimeOffset ReceivedAt = DateTimeOffset.Parse("2026-11-08T01:30:00Z");
    private readonly PandoraWebApplicationFactory _factory;
    private readonly WebApplicationFactory<Program> _host;
    private readonly HttpClient _client;

    public CaptureTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Pandora:DefaultTimeZone"] = "America/Sao_Paulo" }));
            builder.ConfigureTestServices(services => services.AddSingleton<IAiChatCompletionClientFactory, NoAi>());
        });
        _client = _host.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public async Task DisposeAsync() { _client.Dispose(); await _host.DisposeAsync(); }

    [Fact]
    public async Task Creates_a_default_inbox_then_appends_literal_dated_text_with_history_and_search()
    {
        var user = await AuthAsync("capture_basic");
        var first = await CaptureAsync(user, Guid.NewGuid(), "# literal text\nsecond line");
        var second = await CaptureAsync(user, Guid.NewGuid(), "Another capture");
        Assert.Equal(first.PageId, second.PageId);
        Assert.False(second.IsDuplicate);
        var page = await PageAsync(first.PageId);
        Assert.Equal("Captura", page.GetProperty("title").GetString());
        var blocks = page.GetProperty("content").GetProperty("blocks");
        Assert.Equal(2, blocks.GetArrayLength());
        Assert.Equal("paragraph", blocks[0].GetProperty("type").GetString());
        Assert.Equal("[07/11/2026 22:30 -03:00] # literal text\nsecond line", blocks[0].GetProperty("content")[0].GetProperty("text").GetString());
        var spaces = (await _client.GetFromJsonAsync<Envelope<JsonElement>>("/api/v1/notes/spaces"))!.Data;
        var defaultSpace = Assert.Single(spaces.EnumerateArray(), space => space.GetProperty("isDefault").GetBoolean());
        Assert.Equal(defaultSpace.GetProperty("id").GetGuid(), page.GetProperty("spaceId").GetGuid());
        Assert.Contains("assistant", await _client.GetStringAsync($"{Pages}/{first.PageId}/block-stamps"));
        Assert.Equal(2, (await _client.GetFromJsonAsync<Envelope<JsonElement>>($"{Pages}/{first.PageId}/versions"))!.Data.GetArrayLength());
        var hits = (await _client.GetFromJsonAsync<Envelope<JsonElement>>($"{Pages}/search?q=Another"))!.Data;
        Assert.Equal(first.PageId, hits[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Replay_is_a_noop_even_after_the_page_was_purged()
    {
        var user = await AuthAsync("capture_replay");
        var message = Guid.NewGuid();
        var first = await CaptureAsync(user, message, "Keep once");
        var replay = await CaptureAsync(user, message, "Keep once");
        Assert.True(replay.IsDuplicate);
        Assert.Equal(first.PageId, replay.PageId);
        Assert.Equal(1, (await PageAsync(first.PageId)).GetProperty("content").GetProperty("blocks").GetArrayLength());
        Assert.Equal(1, (await _client.GetFromJsonAsync<Envelope<JsonElement>>($"{Pages}/{first.PageId}/versions"))!.Data.GetArrayLength());
        (await _client.DeleteAsync($"{Pages}/{first.PageId}")).EnsureSuccessStatusCode();
        (await _client.DeleteAsync($"/api/v1/notes/trash/{first.PageId}")).EnsureSuccessStatusCode();
        Assert.True((await CaptureAsync(user, message, "Keep once")).IsDuplicate);
        Assert.Equal(0, (await _client.GetFromJsonAsync<Envelope<JsonElement>>(Pages))!.Data.GetArrayLength());
        Assert.NotEqual(first.PageId, (await CaptureAsync(user, Guid.NewGuid(), "Fresh capture")).PageId);
    }

    [Fact]
    public async Task Keeps_a_renamed_inbox_and_never_reuses_someone_elses_page_or_an_unrelated_title()
    {
        var user = await AuthAsync("capture_owner");
        (await _client.PostAsJsonAsync(Pages, new { title = "Captura" })).EnsureSuccessStatusCode();
        var first = await CaptureAsync(user, Guid.NewGuid(), "Owned text");
        var page = await PageAsync(first.PageId);
        Assert.Equal("captura-2", page.GetProperty("slug").GetString());
        (await _client.PutAsJsonAsync($"{Pages}/{first.PageId}", new { title = "My inbox", content = page.GetProperty("content") })).EnsureSuccessStatusCode();
        var spaceResponse = await _client.PostAsJsonAsync("/api/v1/notes/spaces", new { name = "Moved captures" });
        spaceResponse.EnsureSuccessStatusCode();
        var movedSpace = (await spaceResponse.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data.GetProperty("id").GetGuid();
        (await _client.PostAsJsonAsync($"{Pages}/{first.PageId}/move", new { parentId = (Guid?)null, orderIndex = 0, spaceId = movedSpace })).EnsureSuccessStatusCode();
        var next = await CaptureAsync(user, Guid.NewGuid(), "Still here");
        Assert.Equal(first.PageId, next.PageId);
        Assert.Equal("My inbox", next.Title);
        Assert.Equal(movedSpace, (await PageAsync(first.PageId)).GetProperty("spaceId").GetGuid());
        var otherUser = await AuthAsync("capture_other");
        var other = await CaptureAsync(otherUser, Guid.NewGuid(), "Other user");
        Assert.NotEqual(first.PageId, other.PageId);
    }

    [Fact]
    public async Task A_locked_inbox_rejects_capture_without_burning_the_event_receipt()
    {
        var user = await AuthAsync("capture_lock");
        var first = await CaptureAsync(user, Guid.NewGuid(), "First");
        (await _client.PutAsJsonAsync($"{Pages}/{first.PageId}/lock", new { locked = true })).EnsureSuccessStatusCode();
        var message = Guid.NewGuid();
        using (var scope = _host.Services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<INoteCapture>().CaptureAsync(new(user, message, "Second", ReceivedAt, "pt-BR"));
            Assert.Equal("Notes.CaptureLocked", Assert.Single(result.Errors).Code);
        }
        (await _client.PutAsJsonAsync($"{Pages}/{first.PageId}/lock", new { locked = false })).EnsureSuccessStatusCode();
        Assert.False((await CaptureAsync(user, message, "Second")).IsDuplicate);
    }

    [Fact]
    public async Task Telegram_subscriber_captures_without_a_profile_or_AI_key_and_replies_through_outbox()
    {
        var user = await AuthAsync("capture_telegram");
        var message = new InboundMessageReceived(Guid.NewGuid(), ReceivedAt, user, "telegram", "assistant", "/nota saved directly", null, null);
        using var scope = _host.Services.CreateScope();
        var handler = scope.ServiceProvider.GetServices<IIntegrationEventHandler<InboundMessageReceived>>().OfType<InboundMessageReceivedHandler>().Single();
        await handler.HandleAsync(message);
        await handler.HandleAsync(message);
        var pages = (await _client.GetFromJsonAsync<Envelope<JsonElement>>(Pages))!.Data;
        Assert.Equal(1, pages.GetArrayLength());
        var id = pages[0].GetProperty("id").GetGuid();
        Assert.Equal(1, (await PageAsync(id)).GetProperty("content").GetProperty("blocks").GetArrayLength());
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var count = new NpgsqlCommand("SELECT count(*) FROM assistant.tars_outbox_message", connection);
        Assert.Equal(2L, (long)(await count.ExecuteScalarAsync())!);
    }

    [Theory]
    [InlineData("notes.pages.create")]
    [InlineData("notes.pages.update")]
    [InlineData("assistant.commands.run")]
    public async Task Capture_enforces_notes_permissions(string permission)
    {
        var user = await AuthAsync("capture_denied");
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var deny = new NpgsqlCommand("INSERT INTO admin.adm005_user_permission (user_id, pattern, effect) VALUES ($1, $2, 'deny')", connection);
        deny.Parameters.AddWithValue(user);
        deny.Parameters.AddWithValue(permission);
        await deny.ExecuteNonQueryAsync();
        _host.Services.GetRequiredService<IPermissionChecker>().Invalidate(user.ToString());
        using var scope = _host.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<INoteCapture>().CaptureAsync(new(user, Guid.NewGuid(), "Denied", ReceivedAt, "pt-BR"));
        Assert.True(result.IsFailure);
        Assert.Equal(0, (await _client.GetFromJsonAsync<Envelope<JsonElement>>(Pages))!.Data.GetArrayLength());
    }

    private async Task<Guid> AuthAsync(string username)
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);
        return (await _client.GetFromJsonAsync<Envelope<JsonElement>>("/api/v1/identity/me"))!.Data.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Invalid_captures_create_neither_pages_nor_receipts()
    {
        var user = await AuthAsync("capture_invalid");
        using var scope = _host.Services.CreateScope();
        var captures = scope.ServiceProvider.GetRequiredService<INoteCapture>();
        foreach (var text in new[] { "", " ", new string('x', 8001) })
        {
            var result = await captures.CaptureAsync(new(user, Guid.NewGuid(), text, ReceivedAt, "pt-BR"));
            Assert.Equal("Notes.InvalidCapture", Assert.Single(result.Errors).Code);
        }
        var emptyId = await captures.CaptureAsync(new(user, Guid.Empty, "Text", ReceivedAt, "pt-BR"));
        Assert.Equal("Notes.InvalidCapture", Assert.Single(emptyId.Errors).Code);
        Assert.Equal(0, (await _client.GetFromJsonAsync<Envelope<JsonElement>>(Pages))!.Data.GetArrayLength());
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var count = new NpgsqlCommand("SELECT count(*) FROM notes.nte015_note_capture", connection);
        Assert.Equal(0L, (long)(await count.ExecuteScalarAsync())!);
    }

    private async Task<NoteCaptureResult> CaptureAsync(Guid user, Guid message, string text)
    {
        using var scope = _host.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<INoteCapture>().CaptureAsync(new(user, message, text, ReceivedAt, "pt-BR"));
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.Value!;
    }

    private async Task<JsonElement> PageAsync(Guid id) => (await _client.GetFromJsonAsync<Envelope<JsonElement>>($"{Pages}/{id}"))!.Data;
    private sealed record Envelope<T>(T Data);
    private sealed class NoAi : IAiChatCompletionClientFactory
    {
        public IAiChatCompletionClient GetClient(string provider) => throw new InvalidOperationException("A capture must never use AI.");
    }
}
