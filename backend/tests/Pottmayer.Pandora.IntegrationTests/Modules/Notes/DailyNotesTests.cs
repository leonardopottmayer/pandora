using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

/// <summary>Daily-note identity, concurrent creation, template preferences and account-local dates.</summary>
[Collection("Integration")]
public sealed class DailyNotesTests(PandoraWebApplicationFactory factory) : IAsyncLifetime
{
    private const string Pages = "/api/v1/notes/pages";
    private const string Daily = "/api/v1/notes/daily";
    private const string Preferences = "/api/v1/notes/preferences";
    private readonly HttpClient _client = factory.CreateClient();
    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Concurrent_first_visits_create_one_page_folder_and_version()
    {
        await AuthAsync("daily-concurrent");
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => OpenAsync("2026-11-07")));
        var id = results[0].GetProperty("id").GetGuid();
        Assert.All(results, p => Assert.Equal(id, p.GetProperty("id").GetGuid()));
        Assert.Equal("2026-11-07", results[0].GetProperty("title").GetString());
        Assert.Single((await GetAsync(Pages)).EnumerateArray());
        Assert.Single((await GetAsync("/api/v1/notes/folders")).EnumerateArray());
        Assert.Single((await GetAsync($"{Pages}/{id}/versions")).EnumerateArray());
        Assert.Single((await GetAsync("/api/v1/notes/spaces")).EnumerateArray());
    }

    [Fact]
    public async Task Renaming_moving_archiving_and_locking_keep_the_same_daily_identity()
    {
        await AuthAsync("daily-identity");
        var page = await OpenAsync("2026-11-07");
        var id = page.GetProperty("id").GetGuid();
        var space = await ReadAsync(await _client.PostAsJsonAsync("/api/v1/notes/spaces", new { name = "Work" }));
        await ReadAsync(await _client.PutAsJsonAsync($"{Pages}/{id}", new { title = "My journal", content = NoteBlocks.Text("Keep this") }));
        await ReadAsync(await _client.PostAsJsonAsync($"{Pages}/{id}/move", new { spaceId = space.GetProperty("id").GetGuid(), orderIndex = 0 }));
        await ReadAsync(await _client.PostAsync($"{Pages}/{id}/archive", null));
        await ReadAsync(await _client.PutAsJsonAsync($"{Pages}/{id}/lock", new { locked = true }));
        var versions = (await GetAsync($"{Pages}/{id}/versions")).GetRawText();
        var opened = await OpenAsync("2026-11-07");
        Assert.Equal(id, opened.GetProperty("id").GetGuid());
        Assert.Equal("My journal", opened.GetProperty("title").GetString());
        Assert.True(opened.GetProperty("isLocked").GetBoolean());
        Assert.True(opened.GetProperty("isArchived").GetBoolean());
        Assert.Equal(versions, (await GetAsync($"{Pages}/{id}/versions")).GetRawText());
    }

    [Fact]
    public async Task Templates_apply_only_at_creation_and_unavailable_templates_fall_back_to_empty()
    {
        await AuthAsync("daily-template");
        var linked = await ReadAsync(await _client.PostAsJsonAsync(Pages, new { title = "Reference" }));
        Assert.Equal(JsonValueKind.Null, (await GetAsync(Preferences)).GetProperty("dailyTemplateId").ValueKind);
        var model = await ReadAsync(await _client.PostAsJsonAsync(Pages, new { title = "Journal model", icon = "J", content = NoteBlocks.Doc(
            NoteBlocks.Paragraph("{{title}} / {{date}}"), NoteBlocks.Tags("Journal"), NoteBlocks.PageLink(linked.GetProperty("id").GetGuid())) }));
        var modelId = model.GetProperty("id").GetGuid();
        await ReadAsync(await _client.PutAsJsonAsync($"{Pages}/{modelId}/template", new { isTemplate = true }));
        await ReadAsync(await _client.PutAsJsonAsync(Preferences, new { dailyTemplateId = modelId }));
        var page = await OpenAsync("2026-11-07");
        Assert.Contains("2026-11-07 / 2026-11-07", page.GetProperty("content").GetRawText());
        Assert.Equal("J", page.GetProperty("icon").GetString());
        Assert.Single(page.GetProperty("tags").EnumerateArray());
        Assert.Equal(2, (await GetAsync($"{Pages}/{linked.GetProperty("id").GetGuid()}/backlinks")).GetArrayLength());
        Assert.False(page.GetProperty("isTemplate").GetBoolean());
        Assert.NotEqual(model.GetProperty("content").GetProperty("blocks")[0].GetProperty("id").GetString(),
            page.GetProperty("content").GetProperty("blocks")[0].GetProperty("id").GetString());
        await ReadAsync(await _client.PutAsJsonAsync($"{Pages}/{modelId}/template", new { isTemplate = false }));
        var same = await OpenAsync("2026-11-07");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(page.GetProperty("content").GetRawText()),
            JsonNode.Parse(same.GetProperty("content").GetRawText())));
        Assert.Empty((await OpenAsync("2026-11-08")).GetProperty("content").GetProperty("blocks").EnumerateArray());
        await ReadAsync(await _client.PutAsJsonAsync(Preferences, new { dailyTemplateId = (Guid?)null }));
        Assert.Equal(JsonValueKind.Null, (await GetAsync(Preferences)).GetProperty("dailyTemplateId").ValueKind);
    }

    [Fact]
    public async Task The_daily_folder_is_reused_after_rename_and_replaced_after_deletion()
    {
        await AuthAsync("daily-folder");
        _client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pt-BR");
        var folder = await ReadAsync(await _client.PostAsJsonAsync("/api/v1/notes/folders", new { name = "Diário" }));
        var folderId = folder.GetProperty("id").GetGuid();
        var first = await OpenAsync("2026-11-07");
        Assert.Equal(folderId, first.GetProperty("folderId").GetGuid());
        await ReadAsync(await _client.PutAsJsonAsync($"/api/v1/notes/folders/{folderId}", new { name = "Journal" }));
        Assert.Equal(folderId, (await OpenAsync("2026-11-08")).GetProperty("folderId").GetGuid());
        await _client.DeleteAsync($"/api/v1/notes/folders/{folderId}");
        var replacement = await OpenAsync("2026-11-07");
        Assert.NotEqual(folderId, replacement.GetProperty("folderId").GetGuid());
        Assert.NotEqual(first.GetProperty("id").GetGuid(), replacement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task A_deleted_or_purged_daily_page_is_replaced_without_reusing_an_unrelated_title()
    {
        await AuthAsync("daily-trash");
        var ordinary = await ReadAsync(await _client.PostAsJsonAsync(Pages, new { title = "2026-11-07" }));
        var first = await OpenAsync("2026-11-07");
        var firstId = first.GetProperty("id").GetGuid();
        Assert.NotEqual(ordinary.GetProperty("id").GetGuid(), firstId);
        Assert.Equal("2026-11-07-2", first.GetProperty("slug").GetString());
        await _client.DeleteAsync($"{Pages}/{firstId}");
        var replacement = await OpenAsync("2026-11-07");
        var replacementId = replacement.GetProperty("id").GetGuid();
        Assert.NotEqual(firstId, replacementId);
        await ReadAsync(await _client.PostAsync($"/api/v1/notes/trash/{firstId}/restore", null));
        Assert.Equal(replacementId, (await OpenAsync("2026-11-07")).GetProperty("id").GetGuid());
        await _client.DeleteAsync($"{Pages}/{replacementId}");
        await _client.DeleteAsync($"/api/v1/notes/trash/{replacementId}");
        Assert.NotEqual(replacementId, (await OpenAsync("2026-11-07")).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Preferences_reject_foreign_unmarked_and_archived_models_and_daily_notes_are_owner_scoped()
    {
        await AuthAsync("daily-owner");
        var page = await ReadAsync(await _client.PostAsJsonAsync(Pages, new { title = "Model" }));
        var id = page.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync(Preferences, new { dailyTemplateId = id })).StatusCode);
        await ReadAsync(await _client.PutAsJsonAsync($"{Pages}/{id}/template", new { isTemplate = true }));
        await _client.PostAsync($"{Pages}/{id}/archive", null);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync(Preferences, new { dailyTemplateId = id })).StatusCode);
        await _client.PostAsync($"{Pages}/{id}/unarchive", null);
        var ownDaily = await OpenAsync("2026-11-07");
        using var other = factory.CreateClient();
        await IdentityHelper.AuthenticateAsync(other, factory.ConnectionString, "daily-other@example.com", "daily-other");
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync(Preferences, new { dailyTemplateId = id })).StatusCode);
        var otherDaily = await ReadAsync(await other.PostAsJsonAsync(Daily, new { date = "2026-11-07" }));
        Assert.NotEqual(ownDaily.GetProperty("id").GetGuid(), otherDaily.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Today_uses_the_account_day_at_a_UTC_boundary_and_explicit_dates_remain_calendar_dates()
    {
        var clock = new DailyClock();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton<IEffectiveTimeZoneResolver, DailyZone>();
        }));
        using var client = host.CreateClient();
        await IdentityHelper.AuthenticateAsync(client, factory.ConnectionString, "daily-zone@example.com", "daily-zone");
        clock.Now = DateTimeOffset.Parse("2026-11-08T01:30:00Z");
        var today = await ReadAsync(await client.PostAsJsonAsync(Daily, new { }));
        Assert.Equal("2026-11-07", today.GetProperty("title").GetString());
        var specified = await ReadAsync(await client.PostAsJsonAsync(Daily, new { date = "2026-11-08" }));
        Assert.Equal("2026-11-08", specified.GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Daily, new { date = "2026-02-30" })).StatusCode);
    }

    [Theory]
    [InlineData("notes.pages.read")]
    [InlineData("notes.pages.create")]
    [InlineData("notes.folders.create")]
    [InlineData("notes.spaces.read")]
    public async Task Daily_creation_enforces_permissions_before_writing(string permission)
    {
        await AuthAsync("daily-denied");
        var userId = (await GetAsync("/api/v1/identity/me")).GetProperty("id").GetGuid();
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("INSERT INTO admin.adm005_user_permission (user_id, pattern, effect) VALUES ($1, $2, 'deny')", connection);
        command.Parameters.AddWithValue(userId);
        command.Parameters.AddWithValue(permission);
        await command.ExecuteNonQueryAsync();
        factory.Services.GetRequiredService<IPermissionChecker>().Invalidate(userId.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync(Daily, new { })).StatusCode);
    }

    private Task AuthAsync(string user) => IdentityHelper.AuthenticateAsync(_client, factory.ConnectionString, $"{user}@example.com", user);
    private async Task<JsonElement> OpenAsync(string date) => await ReadAsync(await _client.PostAsJsonAsync(Daily, new { date }));
    private async Task<JsonElement> GetAsync(string url) => (await _client.GetFromJsonAsync<Envelope<JsonElement>>(url))!.Data;
    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data;
    }
    private sealed record Envelope<T>(T Data);
    private sealed class DailyClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class DailyZone : IEffectiveTimeZoneResolver
    {
        public Task<TimeZoneInfo> ResolveAsync(Guid userId, string? requested = null, CancellationToken ct = default) => Task.FromResult(ResolveDefault());
        public TimeZoneInfo ResolveDefault() => TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    }
}
