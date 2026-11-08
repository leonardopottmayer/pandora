using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

/// <summary>Read-only folder properties, direct children, filters, ownership and permissions.</summary>
[Collection("Integration")]
public sealed class FolderPropertiesTests(PandoraWebApplicationFactory factory) : IAsyncLifetime
{
    private const string Folders = "/api/v1/notes/folders";
    private const string Pages = "/api/v1/notes/pages";
    private readonly HttpClient _client = factory.CreateClient();
    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Projects_direct_pages_combines_case_variants_and_keeps_repeated_values_without_writing()
    {
        await AuthAsync("properties-table");
        var folder = await CreateAsync(Folders, new { name = "Work" });
        var folderId = folder.GetProperty("id").GetGuid();
        var a = await CreateAsync(Pages, new { title = "A", folderId, content = NoteBlocks.Doc(PropertyBlock(("Status", "Pending"), ("Owner", "Ana"), ("STATUS", "Review"))) });
        var b = await CreateAsync(Pages, new { title = "B", folderId, content = NoteBlocks.Doc(PropertyBlock(("status", "Done"), ("Due", "2026-11-15"))) });
        await CreateAsync(Pages, new { title = "No properties", folderId });
        await CreateAsync(Pages, new { title = "Subpage", parentId = a.GetProperty("id").GetGuid(), content = NoteBlocks.Doc(PropertyBlock(("Child only", "No"))) });
        var subfolder = await CreateAsync(Folders, new { name = "Nested", parentId = folderId });
        await CreateAsync(Pages, new { title = "Nested", folderId = subfolder.GetProperty("id").GetGuid(), content = NoteBlocks.Doc(PropertyBlock(("Nested only", "No"))) });
        var removed = await CreateAsync(Pages, new { title = "Removed", folderId, content = NoteBlocks.Doc(PropertyBlock(("Deleted only", "No"))) });
        await _client.DeleteAsync($"{Pages}/{removed.GetProperty("id").GetGuid()}");
        var aId = a.GetProperty("id").GetGuid();
        var before = (await GetAsync($"{Pages}/{aId}")).GetRawText();
        var versions = (await GetAsync($"{Pages}/{aId}/versions")).GetRawText();

        var result = await GetAsync($"{Folders}/{folderId}/properties");
        Assert.Equal(["Status", "Owner", "Due"], result.GetProperty("columns").EnumerateArray().Select(c => c.GetString()));
        var rows = result.GetProperty("pages").EnumerateArray().ToList();
        Assert.Equal(["A", "B", "No properties"], rows.Select(row => row.GetProperty("page").GetProperty("title").GetString()));
        Assert.Equal("Pending\nReview", rows[0].GetProperty("properties").GetProperty("Status").GetString());
        Assert.Equal("Done", rows[1].GetProperty("properties").GetProperty("Status").GetString());
        Assert.False(rows[1].GetProperty("properties").TryGetProperty("Owner", out _));
        Assert.False(rows[0].GetProperty("page").TryGetProperty("content", out _));
        Assert.Empty(rows[2].GetProperty("properties").EnumerateObject());
        Assert.Equal(before, (await GetAsync($"{Pages}/{aId}")).GetRawText());
        Assert.Equal(versions, (await GetAsync($"{Pages}/{aId}/versions")).GetRawText());
    }

    [Fact]
    public async Task Archive_and_intersecting_tag_filters_limit_both_rows_and_columns()
    {
        await AuthAsync("properties-filters");
        var folderId = (await CreateAsync(Folders, new { name = "Work" })).GetProperty("id").GetGuid();
        var a = await CreateAsync(Pages, new { title = "Both tags", folderId, content = NoteBlocks.Doc(PropertyBlock(("Both", "Yes")), NoteBlocks.Tags("One", "Two")) });
        await CreateAsync(Pages, new { title = "One tag", folderId, content = NoteBlocks.Doc(PropertyBlock(("One only", "Yes")), NoteBlocks.Tags("One")) });
        var archived = await CreateAsync(Pages, new { title = "Archived", folderId, content = NoteBlocks.Doc(PropertyBlock(("Archived only", "Yes"))) });
        await _client.PostAsync($"{Pages}/{archived.GetProperty("id").GetGuid()}/archive", null);
        Assert.Equal(2, (await GetAsync($"{Folders}/{folderId}/properties")).GetProperty("pages").GetArrayLength());
        Assert.Equal(3, (await GetAsync($"{Folders}/{folderId}/properties?includeArchived=true")).GetProperty("pages").GetArrayLength());
        var tags = a.GetProperty("tags").EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).ToList();
        var filtered = await GetAsync($"{Folders}/{folderId}/properties?tagIds={tags[0]}&tagIds={tags[1]}");
        Assert.Single(filtered.GetProperty("pages").EnumerateArray());
        Assert.Equal(["Both"], filtered.GetProperty("columns").EnumerateArray().Select(c => c.GetString()));
        var none = await GetAsync($"{Folders}/{folderId}/properties?tagIds={Guid.NewGuid()}");
        Assert.Empty(none.GetProperty("pages").EnumerateArray());
        Assert.Empty(none.GetProperty("columns").EnumerateArray());
    }

    [Fact]
    public async Task Foreign_or_deleted_folders_return_not_found_and_empty_folders_return_an_empty_projection()
    {
        await AuthAsync("properties-owner");
        var folderId = (await CreateAsync(Folders, new { name = "Empty" })).GetProperty("id").GetGuid();
        var empty = await GetAsync($"{Folders}/{folderId}/properties");
        Assert.Empty(empty.GetProperty("pages").EnumerateArray());
        Assert.Empty(empty.GetProperty("columns").EnumerateArray());
        using var other = factory.CreateClient();
        await IdentityHelper.AuthenticateAsync(other, factory.ConnectionString, "properties-other@example.com", "properties-other");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Folders}/{folderId}/properties")).StatusCode);
        await _client.DeleteAsync($"{Folders}/{folderId}");
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{Folders}/{folderId}/properties")).StatusCode);
    }

    [Theory]
    [InlineData("notes.folders.read", HttpStatusCode.Forbidden)]
    [InlineData("notes.pages.read", HttpStatusCode.Forbidden)]
    [InlineData("notes.pages.update", HttpStatusCode.OK)]
    public async Task Reading_requires_both_read_permissions_but_no_write_permission(string permission, HttpStatusCode expected)
    {
        await AuthAsync("properties-denied");
        var folderId = (await CreateAsync(Folders, new { name = "Work" })).GetProperty("id").GetGuid();
        var userId = (await GetAsync("/api/v1/identity/me")).GetProperty("id").GetGuid();
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("INSERT INTO admin.adm005_user_permission (user_id, pattern, effect) VALUES ($1, $2, 'deny')", connection);
        command.Parameters.AddWithValue(userId);
        command.Parameters.AddWithValue(permission);
        await command.ExecuteNonQueryAsync();
        factory.Services.GetRequiredService<IPermissionChecker>().Invalidate(userId.ToString());
        Assert.Equal(expected, (await _client.GetAsync($"{Folders}/{folderId}/properties")).StatusCode);
    }

    private static object PropertyBlock(params (string Key, string Value)[] rows) => new {
        id = "properties", type = "properties", props = new { rows = rows.Select(row => new {
            key = new[] { new { text = row.Key } }, value = new[] { new { text = row.Value } }
        }) }
    };
    private Task AuthAsync(string user) => IdentityHelper.AuthenticateAsync(_client, factory.ConnectionString, $"{user}@example.com", user);
    private async Task<JsonElement> CreateAsync(string url, object body)
    {
        var response = await _client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data;
    }
    private async Task<JsonElement> GetAsync(string url) => (await _client.GetFromJsonAsync<Envelope<JsonElement>>(url))!.Data;
    private sealed record Envelope<T>(T Data);
}
