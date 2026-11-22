using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

/// <summary>Template designation, independent copies and owner-scoped access through the real API.</summary>
[Collection("Integration")]
public sealed class TemplatesTests(PandoraWebApplicationFactory factory) : IAsyncLifetime
{
    private const string Url = "/api/v1/notes/pages";
    private readonly HttpClient _client = factory.CreateClient();
    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Date_variables_use_the_account_day_at_a_UTC_day_boundary()
    {
        var clock = new TemplateClock();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton<IEffectiveTimeZoneResolver, TemplateZone>();
        }));
        using var client = host.CreateClient();
        await IdentityHelper.AuthenticateAsync(client, factory.ConnectionString, "template-date@example.com", "template-date");
        clock.Now = DateTimeOffset.Parse("2026-11-08T01:30:00Z");
        var model = await ReadAsync(await client.PostAsJsonAsync(Url, new { title = "Daily", content = NoteBlocks.Text("{{date}} / {{title}}") }));
        var id = model.GetProperty("id").GetGuid();
        await ReadAsync(await client.PutAsJsonAsync($"{Url}/{id}/template", new { isTemplate = true }));
        var copy = await ReadAsync(await client.PostAsJsonAsync($"{Url}/from-template", new { templateId = id, title = "Review {{date}}" }));
        Assert.Equal("Review 2026-11-07", copy.GetProperty("title").GetString());
        Assert.Contains("2026-11-07 / Review 2026-11-07", copy.GetProperty("content").GetRawText());
    }

    [Fact]
    public async Task Copies_content_icon_and_derived_tags_without_copying_settings_or_changing_the_template()
    {
        await AuthAsync("template1");
        var model = await CreateAsync(new { title = "Review {{date}}", icon = "R", content = NoteBlocks.Doc(
            NoteBlocks.Paragraph("{{title}} on {{date}}"), NoteBlocks.Tags("Review")) });
        var id = model.GetProperty("id").GetGuid();
        await ReadAsync(await _client.PutAsJsonAsync($"{Url}/{id}/lock", new { locked = true }));
        await ReadAsync(await _client.PutAsJsonAsync($"{Url}/{id}/template", new { isTemplate = true }));
        var original = await GetAsync(id);

        var tree = (await _client.GetFromJsonAsync<Envelope<JsonElement>> (Url))!.Data;
        Assert.True(tree[0].GetProperty("isTemplate").GetBoolean());
        var copy = await ReadAsync(await _client.PostAsJsonAsync($"{Url}/from-template", new { templateId = id, title = "New {{date}}" }));
        Assert.NotEqual(id, copy.GetProperty("id").GetGuid());
        Assert.Equal("R", copy.GetProperty("icon").GetString());
        Assert.False(copy.GetProperty("isTemplate").GetBoolean());
        Assert.False(copy.GetProperty("isLocked").GetBoolean());
        Assert.Null(copy.GetProperty("publicToken").GetString());
        Assert.Equal(1, copy.GetProperty("tags").GetArrayLength());
        var title = copy.GetProperty("title").GetString()!;
        Assert.DoesNotContain("{{date}}", title);
        Assert.Contains(title, copy.GetProperty("content").GetRawText());
        var originalIds = original.GetProperty("content").GetProperty("blocks").EnumerateArray().Select(b => b.GetProperty("id").GetString());
        var copiedIds = copy.GetProperty("content").GetProperty("blocks").EnumerateArray().Select(b => b.GetProperty("id").GetString());
        Assert.Empty(originalIds.Intersect(copiedIds));
        Assert.Equal(original.GetRawText(), (await GetAsync(id)).GetRawText());

        var unmarked = await ReadAsync(await _client.PutAsJsonAsync($"{Url}/{id}/template", new { isTemplate = false }));
        Assert.False(unmarked.GetProperty("isTemplate").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await CopyAsync(id)).StatusCode);
    }

    [Fact]
    public async Task Foreign_unmarked_archived_and_deleted_templates_cannot_be_copied()
    {
        await AuthAsync("template2");
        var page = await CreateAsync(new { title = "Model" });
        var id = page.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await CopyAsync(id)).StatusCode);
        await ReadAsync(await _client.PutAsJsonAsync($"{Url}/{id}/template", new { isTemplate = true }));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.PostAsJsonAsync($"{Url}/from-template", new { templateId = id, title = " " })).StatusCode);
        await _client.PostAsync($"{Url}/{id}/archive", null);
        Assert.Equal(HttpStatusCode.NotFound, (await CopyAsync(id)).StatusCode);
        await _client.PostAsync($"{Url}/{id}/unarchive", null);

        using var other = factory.CreateClient();
        await IdentityHelper.AuthenticateAsync(other, factory.ConnectionString, "template-other@example.com", "template-other");
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"{Url}/from-template", new { templateId = id, title = "Copy" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"{Url}/{id}/template", new { isTemplate = false })).StatusCode);
        await _client.DeleteAsync($"{Url}/{id}");
        Assert.Equal(HttpStatusCode.NotFound, (await CopyAsync(id)).StatusCode);
    }

    private Task AuthAsync(string user) => IdentityHelper.AuthenticateAsync(_client, factory.ConnectionString, $"{user}@example.com", user);
    private async Task<JsonElement> CreateAsync(object body) => await ReadAsync(await _client.PostAsJsonAsync(Url, body));
    private async Task<JsonElement> GetAsync(Guid id) => (await _client.GetFromJsonAsync<Envelope<JsonElement>>($"{Url}/{id}"))!.Data;
    private Task<HttpResponseMessage> CopyAsync(Guid id) => _client.PostAsJsonAsync($"{Url}/from-template", new { templateId = id, title = "Copy" });
    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data;
    }
    private sealed record Envelope<T>(T Data);

    private sealed class TemplateClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class TemplateZone : IEffectiveTimeZoneResolver
    {
        public Task<TimeZoneInfo> ResolveAsync(Guid userId, string? requested = null, CancellationToken ct = default) => Task.FromResult(ResolveDefault());
        public TimeZoneInfo ResolveDefault() => TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    }
}
