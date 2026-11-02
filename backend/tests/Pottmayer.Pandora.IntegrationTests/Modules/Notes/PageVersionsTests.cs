using System.Net;
using System.Net.Http.Json;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Domain.Errors;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

[Collection("Integration")]
public sealed class PageVersionsTests : IAsyncLifetime
{
    private const string Url = "/api/v1/notes/pages";
    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PageVersionsTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Saves_complete_snapshots_skips_identical_saves_and_reads_any_pair()
    {
        await AuthAsync();
        var page = await CreateAsync();
        await SaveAsync(page.Id, "Second", "middle");
        await SaveAsync(page.Id, "Third", "after");
        await SaveAsync(page.Id, "Third", "after");
        Assert.Equal(new long[] { 3, 2, 1 }, (await GetVersionsAsync(page.Id)).Select(v => v.Number));
        Assert.Equal("before", (await GetVersionAsync(page.Id, 1)).Snapshot.ContentMarkdown);
        Assert.Equal("middle", (await GetVersionAsync(page.Id, 2)).Snapshot.ContentMarkdown);
        var last = await GetVersionAsync(page.Id, 3);
        Assert.Equal("Third", last.Snapshot.Title);
        Assert.Equal(page.SpaceId, last.Snapshot.SpaceId);
        Assert.Equal("after", last.Snapshot.ContentMarkdown);
        Assert.Equal(new long[] { 2, 1 }, (await GetVersionsAsync(page.Id, 3)).Select(v => v.Number));
    }

    [Fact]
    public async Task Restore_appends_a_version_and_can_itself_be_undone_with_tags_and_links_rebuilt()
    {
        await AuthAsync();
        var target = await CreateAsync("Target");
        var page = await CreateAsync(body: "[[Target]] #original");
        await SaveAsync(page.Id, "Changed", "#changed");
        var response = await RestoreAsync(page.Id, 1, 2);
        response.EnsureSuccessStatusCode();
        var restored = (await response.Content.ReadFromJsonAsync<Envelope<PageDto>>())!.Data;
        Assert.Equal("Original", restored.Title);
        Assert.Equal("original", Assert.Single(restored.Tags).Slug);
        var backlinks = await _client.GetFromJsonAsync<Envelope<IReadOnlyList<BacklinkDto>>>($"{Url}/{target.Id}/backlinks");
        Assert.Single(backlinks!.Data);
        var snapshot = await GetVersionAsync(page.Id, 3);
        Assert.Equal("restore", snapshot.Source);
        Assert.Equal(1, snapshot.RestoredFrom);
        Assert.Equal(3, (await GetVersionsAsync(page.Id)).Count);
        (await RestoreAsync(page.Id, 2, 3)).EnsureSuccessStatusCode();
        Assert.Equal("#changed", (await GetVersionAsync(page.Id, 4)).Snapshot.ContentMarkdown);
        Assert.Equal("[[Target]] #original", (await GetVersionAsync(page.Id, 1)).Snapshot.ContentMarkdown);
    }

    [Fact]
    public async Task Metadata_changes_and_deletion_are_recorded_and_history_is_retained()
    {
        await AuthAsync();
        var page = await CreateAsync();
        (await _client.PostAsync($"{Url}/{page.Id}/favorite", null)).EnsureSuccessStatusCode();
        (await _client.PostAsync($"{Url}/{page.Id}/archive", null)).EnsureSuccessStatusCode();
        var version = await GetVersionAsync(page.Id, 3);
        Assert.True(version.Snapshot.IsFavorite);
        Assert.NotNull(version.Snapshot.ArchivedAt);
        (await RestoreAsync(page.Id, 1, 3)).EnsureSuccessStatusCode();
        var plain = (await _client.GetFromJsonAsync<Envelope<PageDto>>($"{Url}/{page.Id}"))!.Data;
        Assert.False(plain.IsFavorite);
        Assert.False(plain.IsArchived);
        (await _client.DeleteAsync($"{Url}/{page.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{Url}/{page.Id}/versions")).StatusCode);
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM notes.nte011_page_version WHERE page_id = @id", connection);
        command.Parameters.AddWithValue("id", page.Id);
        Assert.Equal(5L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Stale_restore_is_rejected_without_changing_page_or_history()
    {
        await AuthAsync();
        var page = await CreateAsync();
        await SaveAsync(page.Id, "Newer", "do not overwrite");
        Assert.Equal(HttpStatusCode.Conflict, (await RestoreAsync(page.Id, 1, 1)).StatusCode);
        Assert.Equal(2, (await GetVersionsAsync(page.Id)).Count);
        Assert.Equal("do not overwrite", (await GetVersionAsync(page.Id, 2)).Snapshot.ContentMarkdown);
    }

    [Fact]
    public async Task History_and_restore_are_scoped_to_the_owner_and_page()
    {
        await AuthAsync();
        var page = await CreateAsync();
        await AuthAsync("intruder");
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{Url}/{page.Id}/versions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{Url}/{page.Id}/versions/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await RestoreAsync(page.Id, 1, 1)).StatusCode);
        var own = await CreateAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{Url}/{own.Id}/versions/999")).StatusCode);
    }

    [Fact]
    public async Task Restore_rejects_cycles_created_since_the_original_version()
    {
        await AuthAsync();
        var parent = await CreateAsync("Parent");
        var child = await CreateAsync("Child", parentId: parent.Id);
        (await _client.PostAsJsonAsync($"{Url}/{child.Id}/move", new { parentId = (Guid?)null, orderIndex = 0 })).EnsureSuccessStatusCode();
        (await _client.PostAsJsonAsync($"{Url}/{parent.Id}/move", new { parentId = child.Id, orderIndex = 0 })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await RestoreAsync(child.Id, 1, 2)).StatusCode);
        Assert.Equal(2, (await GetVersionsAsync(child.Id)).Count);
    }

    [Fact]
    public async Task Concurrent_saves_commit_one_complete_version_and_reject_the_stale_writer()
    {
        await AuthAsync();
        var page = await CreateAsync();
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT user_id FROM notes.nte001_page WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", page.Id);
        var userId = (Guid)(await command.ExecuteScalarAsync())!;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readers = 0;
        async Task<bool> WriteAsync(string text)
        {
            using var scope = _factory.Services.CreateScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>().ExecuteAsync(NotesModule.DatabaseKey, async (ctx, ct) =>
                {
                    var repo = ctx.AcquireRepository<IPageRepository>();
                    var loaded = (await repo.FindByIdForUserAsync(page.Id, userId, ct))!;
                    if (Interlocked.Increment(ref readers) == 2)
                        ready.SetResult();
                    await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    loaded.Update(text, null, text);
                    await repo.UpdateAsync(loaded, ct);
                    return true;
                });
                return true;
            }
            catch (PageHistoryConflictException)
            {
                return false;
            }
        }
        var results = await Task.WhenAll(WriteAsync("first"), WriteAsync("second"));
        Assert.Single(results, success => success);
        Assert.Equal(2, (await GetVersionsAsync(page.Id)).Count);
        var latest = await GetVersionAsync(page.Id, 2);
        Assert.Equal(latest.Snapshot.Title, latest.Snapshot.ContentMarkdown);
    }

    [Fact]
    public async Task Restoring_a_space_move_brings_descendants_back_and_versions_their_changes()
    {
        await AuthAsync();
        var root = await CreateAsync("Root");
        var child = await CreateAsync("Child", parentId: root.Id);
        var spaceResponse = await _client.PostAsJsonAsync("/api/v1/notes/spaces", new { name = "Other" });
        spaceResponse.EnsureSuccessStatusCode();
        var space = (await spaceResponse.Content.ReadFromJsonAsync<Envelope<SpaceDto>>())!.Data;
        (await _client.PostAsJsonAsync($"{Url}/{root.Id}/move", new { spaceId = space.Id, parentId = (Guid?)null, orderIndex = 4 })).EnsureSuccessStatusCode();
        Assert.Equal(space.Id, (await GetVersionAsync(child.Id, 2)).Snapshot.SpaceId);
        (await RestoreAsync(root.Id, 1, 2)).EnsureSuccessStatusCode();
        Assert.Equal(root.SpaceId, (await GetVersionAsync(root.Id, 3)).Snapshot.SpaceId);
        Assert.Equal(root.SpaceId, (await GetVersionAsync(child.Id, 3)).Snapshot.SpaceId);
        Assert.Equal(root.Id, (await GetVersionAsync(child.Id, 3)).Snapshot.ParentId);
    }

    [Fact]
    public async Task Migration_backfills_existing_pages_with_their_full_current_state()
    {
        await AuthAsync();
        var page = await CreateAsync(body: "Existing markdown");
        (await _client.PostAsync($"{Url}/{page.Id}/favorite", null)).EnsureSuccessStatusCode();
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "migrations", "migrations"))) directory = directory.Parent;
        var migrationDirectory = Path.Combine(directory!.FullName, "migrations", "migrations", "notes", "20261102140000-create-table-nte011-page-version");
        var down = await File.ReadAllTextAsync(Path.Combine(migrationDirectory, "20261102140000-create-table-nte011-page-version.down.sql"));
        var up = await File.ReadAllTextAsync(Path.Combine(migrationDirectory, "20261102140000-create-table-nte011-page-version.up.sql"));
        // Only the disposable integration database is changed; rebuild over populated old rows.
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand(
            down + "\nUPDATE notes.nte001_page SET history_number = 1;\n" + up, connection, transaction);
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
        var baseline = await GetVersionAsync(page.Id, 1);
        Assert.Equal("initial", baseline.Source);
        Assert.Equal("Existing markdown", baseline.Snapshot.ContentMarkdown);
        Assert.True(baseline.Snapshot.IsFavorite);
        Assert.Equal(page.SpaceId, baseline.Snapshot.SpaceId);
        await SaveAsync(page.Id, "After migration", "new");
        Assert.Equal(2, (await GetVersionsAsync(page.Id)).Count);
    }

    private Task AuthAsync(string name = "versions") => IdentityHelper.AuthenticateAsync(
        _client, _factory.ConnectionString, $"{name}@example.com", name);

    private async Task<PageDto> CreateAsync(string title = "Original", string body = "before", Guid? parentId = null)
    {
        var response = await _client.PostAsJsonAsync(Url, new { title, contentMarkdown = body, parentId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Envelope<PageDto>>())!.Data;
    }

    private async Task SaveAsync(Guid id, string title, string body)
        => (await _client.PutAsJsonAsync($"{Url}/{id}", new { title, contentMarkdown = body })).EnsureSuccessStatusCode();

    private async Task<IReadOnlyList<PageVersionSummaryDto>> GetVersionsAsync(Guid id, long? before = null)
        => (await _client.GetFromJsonAsync<Envelope<IReadOnlyList<PageVersionSummaryDto>>>($"{Url}/{id}/versions?before={before}"))!.Data;

    private async Task<PageVersionDto> GetVersionAsync(Guid id, long number)
        => (await _client.GetFromJsonAsync<Envelope<PageVersionDto>>($"{Url}/{id}/versions/{number}"))!.Data;

    private Task<HttpResponseMessage> RestoreAsync(Guid id, long number, long expectedVersion)
        => _client.PostAsJsonAsync($"{Url}/{id}/versions/{number}/restore", new { expectedVersion });

    private sealed record Envelope<T>(T Data);
}
