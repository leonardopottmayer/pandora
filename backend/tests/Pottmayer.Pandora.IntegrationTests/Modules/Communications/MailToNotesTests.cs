using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Communications;

/// <summary>Copies owned mail through the Notes port, keeping permissions, source links and normal page history.</summary>
[Collection("Integration")]
public sealed class MailToNotesTests : IAsyncLifetime
{
    private const string Mail = "/api/v1/communications";
    private const string Notes = "/api/v1/notes";
    private readonly PandoraWebApplicationFactory _factory;
    private readonly FakeMailProvider _provider = new();
    private readonly WebApplicationFactory<Program> _host;
    private readonly HttpClient _client;

    public MailToNotesTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddKeyedSingleton<IMailboxProvider>(MailProvider.Titan.Value, _provider)));
        _client = _host.CreateClient();
    }
    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Saves_literal_text_in_the_chosen_folder_with_source_history_and_search_without_copying_attachments()
    {
        var id = await SeedAsync("mail-notes");
        await CacheAsync(id, "# literal\n- Keep this\ncopycheck <script>plain text</script>", "<p>Ignored alternative</p>");
        var folder = await ReadAsync(await _client.PostAsJsonAsync($"{Notes}/folders", new { name = "Mail archive" }));
        var folderId = folder.GetProperty("id").GetGuid();
        var copiedId = await SaveAsync(id, new { title = "Copied mail", folderId });
        var page = await GetAsync($"{Notes}/pages/{copiedId}");
        Assert.Equal("Copied mail", page.GetProperty("title").GetString());
        Assert.Equal(folderId, page.GetProperty("folderId").GetGuid());
        var blocks = page.GetProperty("content").GetProperty("blocks").EnumerateArray().ToList();
        Assert.Equal(["paragraph", "paragraph", "paragraph", "bookmark"], blocks.Select(b => b.GetProperty("type").GetString()));
        Assert.Equal("# literal", blocks[0].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal($"/mail?message={id}", blocks[^1].GetProperty("props").GetProperty("url").GetString());
        Assert.Equal(blocks.Count, blocks.Select(b => b.GetProperty("id").GetString()).Distinct().Count());
        Assert.Empty(page.GetProperty("tags").EnumerateArray());
        Assert.Single((await GetAsync($"{Notes}/pages/{copiedId}/versions")).EnumerateArray());
        Assert.Single((await GetAsync($"{Notes}/pages/search?q=copycheck")).EnumerateArray());
        Assert.Empty((await GetAsync($"{Notes}/media")).EnumerateArray());
        Assert.True((await GetAsync($"{Mail}/messages/{id}")).GetProperty("message").GetProperty("isUnread").GetBoolean());
        Assert.Empty(_provider.Sent);
    }

    [Fact]
    public async Task Html_only_mail_becomes_text_without_scripts_styles_or_remote_images()
    {
        var id = await SeedAsync("mail-html");
        await CacheAsync(id, null, "<style>SECRET STYLE</style><p>Hello &amp; world</p><p>Second line</p><script>SECRET SCRIPT</script><img src='https://tracker.test'>");
        var noteId = await SaveAsync(id, new { });
        var blocks = (await GetAsync($"{Notes}/pages/{noteId}")).GetProperty("content").GetProperty("blocks");
        var paragraphs = blocks.EnumerateArray().Where(b => b.GetProperty("type").GetString() == "paragraph")
            .Select(b => b.GetProperty("content")[0].GetProperty("text").GetString()).ToList();
        Assert.Contains("Hello & world", paragraphs);
        Assert.Contains("Second line", paragraphs);
        Assert.DoesNotContain(paragraphs, text => text!.Contains("SECRET") || text.Contains("tracker.test"));
    }

    [Fact]
    public async Task An_uncached_message_uses_the_existing_body_reader_and_imports_no_attachment()
    {
        var id = await SeedAsync("mail-uncached");
        var before = _provider.RawFetches;
        var noteId = await SaveAsync(id, new { });
        Assert.Equal(before + 1, _provider.RawFetches);
        Assert.Contains("Text of Original subject", (await GetAsync($"{Notes}/pages/{noteId}")).GetProperty("content").GetRawText());
        Assert.Empty((await GetAsync($"{Notes}/media")).EnumerateArray());
    }

    [Fact]
    public async Task Foreign_messages_and_destinations_are_not_found_and_create_no_note()
    {
        var id = await SeedAsync("mail-owner");
        using var other = _host.CreateClient();
        await IdentityHelper.AuthenticateAsync(other, _factory.ConnectionString, "mail-other@example.com", "mail-other");
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"{Mail}/messages/{id}/notes", new { })).StatusCode);
        var foreignFolder = (await ReadAsync(await other.PostAsJsonAsync($"{Notes}/folders", new { name = "Other" }))).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync($"{Mail}/messages/{id}/notes", new { folderId = foreignFolder })).StatusCode);
        Assert.Empty((await GetAsync($"{Notes}/pages")).EnumerateArray());
    }

    [Theory]
    [InlineData("communications.messages.read")]
    [InlineData("notes.pages.create")]
    [InlineData("notes.pages.read")]
    [InlineData("notes.spaces.read")]
    [InlineData("notes.folders.read")]
    public async Task Missing_permissions_refuse_before_fetching_or_creating(string permission)
    {
        var id = await SeedAsync("mail-denied");
        var userId = (await GetAsync("/api/v1/identity/me")).GetProperty("id").GetGuid();
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("INSERT INTO admin.adm005_user_permission (user_id, pattern, effect) VALUES ($1, $2, 'deny')", connection);
        command.Parameters.AddWithValue(userId);
        command.Parameters.AddWithValue(permission);
        await command.ExecuteNonQueryAsync();
        _host.Services.GetRequiredService<IPermissionChecker>().Invalidate(userId.ToString());
        var before = _provider.RawFetches;
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync($"{Mail}/messages/{id}/notes", new { })).StatusCode);
        Assert.Equal(before, _provider.RawFetches);
    }

    [Fact]
    public async Task Invalid_titles_and_oversized_content_create_no_partial_page()
    {
        var id = await SeedAsync("mail-limits");
        foreach (var title in new[] { " ", new string('a', 256) })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.PostAsJsonAsync($"{Mail}/messages/{id}/notes", new { title })).StatusCode);
        await CacheAsync(id, new string('x', 2 * 1024 * 1024), null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.PostAsJsonAsync($"{Mail}/messages/{id}/notes", new { })).StatusCode);
        Assert.Empty((await GetAsync($"{Notes}/pages")).EnumerateArray());
    }

    private async Task<Guid> SeedAsync(string user)
    {
        _provider.Deliver("m1", "Original subject");
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{user}@example.com", user);
        var account = await ReadAsync(await _client.PutAsJsonAsync("/api/v1/integrations/titan/password", new { username = "me@titan.test", password = "secret" }));
        var mailbox = await ReadAsync(await _client.PostAsJsonAsync($"{Mail}/mailboxes", new { provider = "titan", externalAccountId = account.GetGuid() }));
        await ReadAsync(await _client.PostAsync($"{Mail}/mailboxes/{mailbox.GetProperty("id").GetGuid()}/sync", null));
        return (await GetAsync($"{Mail}/messages")).GetProperty("items")[0].GetProperty("id").GetGuid();
    }
    private async Task CacheAsync(Guid id, string? text, string? html)
    {
        using var scope = _host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>().ExecuteAsync(CommunicationsModule.DatabaseKey, async (ctx, ct) =>
        {
            var repo = ctx.AcquireRepository<IMailContentRepository>();
            var old = await repo.GetByIdAsync(id, ct);
            if (old is not null) await repo.RemoveAsync(old, ct);
        });
        await scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>().ExecuteAsync(CommunicationsModule.DatabaseKey, async (ctx, ct) =>
        {
            var repo = ctx.AcquireRepository<IMailContentRepository>();
            await repo.AddAsync(MailContent.Create(id, text, html, [], TimeProvider.System.GetUtcNow()), ct);
        });
    }
    private async Task<Guid> SaveAsync(Guid id, object body) => (await ReadAsync(await _client.PostAsJsonAsync($"{Mail}/messages/{id}/notes", body))).GetGuid();
    private async Task<JsonElement> GetAsync(string url) => (await _client.GetFromJsonAsync<Envelope<JsonElement>>(url))!.Data;
    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data;
    }
    private sealed record Envelope<T>(T Data);
}
