using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Tars.Ai.Abstractions;
using Pottmayer.Tars.Ai.Chat.Abstractions;
using Pottmayer.Tars.Ai.Chat.Abstractions.Models;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Notes;

/// <summary>Writing previews through the real Notes/Assistant boundary, with only the provider replaced.</summary>
[Collection("Integration")]
public sealed class AiTests : IAsyncLifetime
{
    private const string Url = "/api/v1/notes/ai/transform";
    private const string Pages = "/api/v1/notes/pages";
    private readonly PandoraWebApplicationFactory _factory;
    private readonly WebApplicationFactory<Program> _host;
    private readonly HttpClient _client;
    private readonly FakeChat _chat = new();

    public AiTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton<IAiChatCompletionClientFactory>(_chat);
            s.AddSingleton<IExternalCredentialProvider, FakeCredentials>();
        }));
        _client = _host.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public async Task DisposeAsync() { _client.Dispose(); await _host.DisposeAsync(); }

    [Fact]
    public async Task Requires_authentication_and_an_enabled_assistant()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await TransformAsync()).StatusCode);
        await AuthAsync("ai_disabled", enabled: false);
        var response = await TransformAsync();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("Assistant.NotEnabled", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, _chat.Calls);
    }

    [Fact]
    public async Task Previews_without_saving_then_records_only_accepted_changes_as_assistant()
    {
        await AuthAsync("ai_preview");
        var created = await _client.PostAsJsonAsync(Pages, new { title = "Note", content = Doc("Original") });
        var pageId = (await created.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data.GetProperty("id").GetGuid();
        var before = (await _client.GetFromJsonAsync<Envelope<JsonElement>>($"{Pages}/{pageId}/versions"))!.Data.GetRawText();

        var response = await TransformAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Generated text", (await response.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data.GetProperty("text").GetString());
        Assert.Equal(before, (await _client.GetFromJsonAsync<Envelope<JsonElement>>($"{Pages}/{pageId}/versions"))!.Data.GetRawText());
        Assert.Contains("Original", await _client.GetStringAsync($"{Pages}/{pageId}"));
        Assert.Equal("chosen-model", _chat.LastRequest!.Model);
        Assert.Equal("fake-key", _chat.LastRequest.ApiKey);
        Assert.Null(_chat.LastRequest.Tools);

        var saved = await _client.PutAsJsonAsync($"{Pages}/{pageId}", new { title = "Note", content = Doc("Generated text"), source = "assistant" });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Contains("assistant", await _client.GetStringAsync($"{Pages}/{pageId}/block-stamps"));
        Assert.Contains("assistant", await _client.GetStringAsync($"{Pages}/{pageId}/versions"));

        saved = await _client.PutAsJsonAsync($"{Pages}/{pageId}", new { title = "Note", content = Doc("Manual edit") });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.DoesNotContain("assistant", await _client.GetStringAsync($"{Pages}/{pageId}/block-stamps"));
        var invalid = await _client.PutAsJsonAsync($"{Pages}/{pageId}", new { title = "Note", content = Doc("Bad source"), source = "restore" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
    }

    [Fact]
    public async Task Invalid_inputs_never_reach_the_provider()
    {
        await AuthAsync("ai_invalid");
        foreach (var input in new[] {
            new { action = "unknown", text = "Text", language = "English" },
            new { action = "translate", text = "Text", language = "" },
            new { action = "summarize", text = new string('x', 8001), language = "" } })
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.PostAsJsonAsync(Url, input)).StatusCode);
        }
        Assert.Equal(0, _chat.Calls);
    }

    [Fact]
    public async Task Provider_errors_are_friendly_and_localized()
    {
        await AuthAsync("ai_error");
        _chat.Fail = true;
        _client.DefaultRequestHeaders.Add("Accept-Language", "pt-BR");
        var response = await TransformAsync();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Assistant.TextCompletionFailed", body);
        Assert.Contains("Tente novamente", body);
        Assert.DoesNotContain("provider details", body);
    }

    [Theory]
    [InlineData("assistant.commands.run")]
    [InlineData("notes.pages.update")]
    public async Task Both_modules_permissions_are_enforced(string permission)
    {
        var userId = await AuthAsync("ai_denied");
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("INSERT INTO admin.adm005_user_permission (user_id, pattern, effect) VALUES ($1, $2, 'deny')", connection);
        command.Parameters.AddWithValue(userId);
        command.Parameters.AddWithValue(permission);
        await command.ExecuteNonQueryAsync();
        _host.Services.GetRequiredService<IPermissionChecker>().Invalidate(userId.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await TransformAsync()).StatusCode);
        Assert.Equal(0, _chat.Calls);
    }

    private Task<HttpResponseMessage> TransformAsync() => _client.PostAsJsonAsync(Url, new { action = "improve", text = "Original" });

    [Fact]
    public async Task Page_summary_reads_saved_text_and_table_cells_without_creating_a_version()
    {
        await AuthAsync("ai_summary");
        var created = await _client.PostAsJsonAsync(Pages, new
        {
            title = "Project decisions",
            content = NoteBlocks.Doc(
                new { id = "p", type = "paragraph", content = new[] { new { text = new string('x', 9000) } } },
                new { id = "t", type = "table", props = new { rows = new[] { new[] { new[] { new { text = "Decision in table" } } } } } })
        });
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data.GetProperty("id").GetGuid();
        var before = (await _client.GetFromJsonAsync<Envelope<JsonElement>>($"{Pages}/{id}/versions"))!.Data.GetRawText();
        var response = await _client.PostAsync($"{Pages}/{id}/ai/summary", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var sent = JsonDocument.Parse(_chat.LastRequest!.Messages.Last().Content!);
        Assert.Equal("Project decisions", sent.RootElement.GetProperty("title").GetString());
        Assert.Contains("Decision in table", sent.RootElement.GetProperty("text").GetString());
        Assert.Contains(new string('x', 9000), sent.RootElement.GetProperty("text").GetString());
        Assert.Equal(before, (await _client.GetFromJsonAsync<Envelope<JsonElement>>($"{Pages}/{id}/versions"))!.Data.GetRawText());
    }

    [Fact]
    public async Task Page_summary_never_reads_another_owners_or_deleted_page()
    {
        await AuthAsync("ai_summary_owner");
        var created = await _client.PostAsJsonAsync(Pages, new { title = "Private", content = Doc("Private content") });
        var id = (await created.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data.GetProperty("id").GetGuid();
        await AuthAsync("ai_summary_other");
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync($"{Pages}/{id}/ai/summary", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync($"{Pages}/{Guid.NewGuid()}/ai/summary", null)).StatusCode);
        var own = await _client.PostAsJsonAsync(Pages, new { title = "Deleted", content = Doc("Deleted content") });
        var ownId = (await own.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data.GetProperty("id").GetGuid();
        (await _client.DeleteAsync($"{Pages}/{ownId}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync($"{Pages}/{ownId}/ai/summary", null)).StatusCode);
        Assert.Equal(0, _chat.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32001)]
    public async Task Empty_or_oversized_page_summaries_do_not_call_the_provider(int length)
    {
        await AuthAsync("ai_summary_limit");
        var created = await _client.PostAsJsonAsync(Pages, new { title = "Note", content = Doc(new string('x', length)) });
        var id = (await created.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data.GetProperty("id").GetGuid();
        var response = await _client.PostAsync($"{Pages}/{id}/ai/summary", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("Notes.InvalidPageSummary", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, _chat.Calls);
    }
    private static object Doc(string text) => NoteBlocks.Doc(new { id = "b1", type = "paragraph", content = new[] { new { text } } });

    [Fact]
    public async Task AI_block_generation_is_a_preview_and_accepted_output_is_searched_and_versioned()
    {
        await AuthAsync("ai_block");
        var created = await _client.PostAsJsonAsync(Pages, new { title = "AI page", content = NoteBlocks.Doc(new
        {
            id = "ai1", type = "ai", props = new { prompt = "Explain an outbox", output = new[] { new { text = "Old output" } } }
        }) });
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data.GetProperty("id").GetGuid();
        var before = (await _client.GetFromJsonAsync<Envelope<JsonElement>>($"{Pages}/{id}/versions"))!.Data.GetRawText();
        var response = await _client.PostAsJsonAsync("/api/v1/notes/ai/generate", new { prompt = "Explain an outbox" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var generated = (await response.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data;
        var output = generated.GetProperty("text").GetString()!;
        var generatedAt = generated.GetProperty("generatedAt").GetDateTimeOffset();
        Assert.Equal(TimeSpan.Zero, generatedAt.Offset);
        Assert.Equal("Explain an outbox", _chat.LastRequest!.Messages.Last().Content);
        Assert.Null(_chat.LastRequest.Tools);
        Assert.Equal(before, (await _client.GetFromJsonAsync<Envelope<JsonElement>>($"{Pages}/{id}/versions"))!.Data.GetRawText());
        Assert.Contains("Old output", await _client.GetStringAsync($"{Pages}/{id}"));

        var content = NoteBlocks.Doc(new { id = "ai1", type = "ai", props = new { prompt = "Explain an outbox", output = new[] { new { text = output } }, generatedAt } });
        (await _client.PutAsJsonAsync($"{Pages}/{id}", new { title = "AI page", content, source = "assistant" })).EnsureSuccessStatusCode();
        var saved = (await _client.GetFromJsonAsync<Envelope<JsonElement>>($"{Pages}/{id}"))!.Data.GetProperty("content").GetProperty("blocks")[0];
        Assert.Equal("ai1", saved.GetProperty("id").GetString());
        Assert.Equal(output, saved.GetProperty("props").GetProperty("output")[0].GetProperty("text").GetString());
        Assert.Equal(generatedAt, saved.GetProperty("props").GetProperty("generatedAt").GetDateTimeOffset());
        Assert.Contains("assistant", await _client.GetStringAsync($"{Pages}/{id}/block-stamps"));
        var hits = (await _client.GetFromJsonAsync<Envelope<JsonElement>>($"{Pages}/search?q=Generated"))!.Data;
        Assert.Equal(id, hits[0].GetProperty("id").GetGuid());
        var versions = (await _client.GetFromJsonAsync<Envelope<JsonElement>>($"{Pages}/{id}/versions"))!.Data;
        Assert.Equal(2, versions.GetArrayLength());
    }

    [Fact]
    public async Task Generation_rejects_invalid_prompts_without_calling_AI()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/v1/notes/ai/generate", new { prompt = "Prompt" })).StatusCode);
        await AuthAsync("ai_block_invalid");
        foreach (var prompt in new[] { "", " ", new string('x', 8001) })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.PostAsJsonAsync("/api/v1/notes/ai/generate", new { prompt })).StatusCode);
        Assert.Equal(0, _chat.Calls);
    }

    [Fact]
    public async Task Ask_uses_only_the_owners_matching_saved_pages_without_writing_versions()
    {
        await AuthAsync("ask_other");
        await _client.PostAsJsonAsync(Pages, new { title = "Apollo secret", content = Doc("Apollo private secret from someone else") });
        await AuthAsync("ask_owner");
        var created = await _client.PostAsJsonAsync(Pages, new { title = "Apollo launch", content = Doc("Apollo deadline is March 20.") });
        var page = (await created.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data;
        var id = page.GetProperty("id").GetGuid();
        var before = (await _client.GetFromJsonAsync<Envelope<JsonElement>>($"{Pages}/{id}/versions"))!.Data.GetRawText();
        _chat.Answer = request =>
        {
            Assert.DoesNotContain("private secret", request.Messages.Last().Content);
            using var json = JsonDocument.Parse(request.Messages.Last().Content!);
            var sources = json.RootElement.GetProperty("sources");
            Assert.Equal(1, sources.GetArrayLength());
            Assert.Contains("March 20", sources[0].GetProperty("text").GetString());
            return JsonSerializer.Serialize(new { answer = "The deadline is March 20.", pageIds = new[] { id } });
        };
        var response = await _client.PostAsJsonAsync("/api/v1/notes/ai/ask", new { question = "What is the Apollo deadline?" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data;
        Assert.Equal("The deadline is March 20.", result.GetProperty("answer").GetString());
        Assert.Equal(id, result.GetProperty("sources")[0].GetProperty("pageId").GetGuid());
        Assert.Equal(before, (await _client.GetFromJsonAsync<Envelope<JsonElement>>($"{Pages}/{id}/versions"))!.Data.GetRawText());

        var filtered = await _client.PostAsJsonAsync("/api/v1/notes/ai/ask", new { question = "Apollo deadline", spaceId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await filtered.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data.GetProperty("answer").ValueKind);
        Assert.Equal(1, _chat.Calls);
    }

    [Fact]
    public async Task Ask_ignores_deleted_pages_and_returns_no_information_without_calling_AI()
    {
        await AuthAsync("ask_deleted");
        var created = await _client.PostAsJsonAsync(Pages, new { title = "Apollo", content = Doc("Apollo deadline") });
        var id = (await created.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data.GetProperty("id").GetGuid();
        (await _client.DeleteAsync($"{Pages}/{id}")).EnsureSuccessStatusCode();
        var response = await _client.PostAsJsonAsync("/api/v1/notes/ai/ask", new { question = "Apollo deadline" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await response.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data.GetProperty("answer").ValueKind);
        Assert.Equal(0, _chat.Calls);
    }

    [Fact]
    public async Task Ask_validates_provider_sources_and_handles_insufficient_evidence()
    {
        await AuthAsync("ask_sources");
        await _client.PostAsJsonAsync(Pages, new { title = "Apollo", content = Doc("Apollo deadline in March") });
        _chat.Reply = JsonSerializer.Serialize(new { answer = "Invented", pageIds = new[] { Guid.NewGuid() } });
        var response = await _client.PostAsJsonAsync("/api/v1/notes/ai/ask", new { question = "Apollo deadline" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("Notes.InvalidAnswer", await response.Content.ReadAsStringAsync());
        _chat.Reply = "{\"answer\":null,\"pageIds\":[]}";
        response = await _client.PostAsJsonAsync("/api/v1/notes/ai/ask", new { question = "Apollo budget" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await response.Content.ReadFromJsonAsync<Envelope<JsonElement>>())!.Data.GetProperty("answer").ValueKind);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Ask_rejects_empty_questions(string question)
    {
        await AuthAsync("ask_invalid");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.PostAsJsonAsync("/api/v1/notes/ai/ask", new { question })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _client.PostAsJsonAsync("/api/v1/notes/ai/ask", new { question = new string('x', 1001) })).StatusCode);
        Assert.Equal(0, _chat.Calls);
    }

    [Theory]
    [InlineData("notes.pages.read")]
    [InlineData("assistant.commands.run")]
    public async Task Ask_enforces_permissions_even_when_no_page_matches(string permission)
    {
        var userId = await AuthAsync("ask_denied");
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("INSERT INTO admin.adm005_user_permission (user_id, pattern, effect) VALUES ($1, $2, 'deny')", connection);
        command.Parameters.AddWithValue(userId);
        command.Parameters.AddWithValue(permission);
        await command.ExecuteNonQueryAsync();
        _host.Services.GetRequiredService<IPermissionChecker>().Invalidate(userId.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync("/api/v1/notes/ai/ask", new { question = "Apollo deadline" })).StatusCode);
        Assert.Equal(0, _chat.Calls);
    }

    [Fact]
    public async Task Ask_bounds_the_number_and_size_of_passages()
    {
        await AuthAsync("ask_bounds");
        for (var i = 0; i < 6; i++)
            (await _client.PostAsJsonAsync(Pages, new { title = $"Apollo {i}", content = Doc("Apollo context " + new string('x', 9000)) })).EnsureSuccessStatusCode();
        _chat.Answer = request =>
        {
            using var json = JsonDocument.Parse(request.Messages.Last().Content!);
            var sources = json.RootElement.GetProperty("sources");
            Assert.Equal(5, sources.GetArrayLength());
            Assert.All(sources.EnumerateArray(), source => Assert.InRange(source.GetProperty("text").GetString()!.Length, 1, 4002));
            return JsonSerializer.Serialize(new { answer = "Context", pageIds = new[] { sources[0].GetProperty("pageId").GetGuid() } });
        };
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync("/api/v1/notes/ai/ask", new { question = "Apollo context" })).StatusCode);
    }

    private async Task<Guid> AuthAsync(string username, bool enabled = true)
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);
        var me = await _client.GetFromJsonAsync<Envelope<JsonElement>>("/api/v1/identity/me");
        var userId = me!.Data.GetProperty("id").GetGuid();
        using var scope = _host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>().ExecuteAsync(AssistantModule.DatabaseKey, async (context, ct) =>
        {
            await context.AcquireRepository<IAssistantProfileRepository>().AddAsync(AssistantProfile.Create(userId,
                "gemini", Guid.NewGuid(), "chosen-model", enabled, null, ConfirmationLevel.Balanced, TimeProvider.System), ct);
        });
        return userId;
    }

    private sealed class FakeChat : IAiChatCompletionClientFactory, IAiChatCompletionClient
    {
        public string? Reply { get; set; }
        public Func<ChatRequest, string>? Answer { get; set; }
        public bool Fail { get; set; }
        public int Calls { get; private set; }
        public ChatRequest? LastRequest { get; private set; }
        public IAiChatCompletionClient GetClient(string provider) => this;
        public Task<ChatCompletion> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            if (Fail) throw new AiException("gemini", "provider details", isPermanent: false);
            return Task.FromResult(new ChatCompletion(request.Model, new ChatMessage(ChatRole.Assistant,
                Answer?.Invoke(request) ?? Reply ?? "Generated text"), new TokenUsage(1, 1)));
        }
    }

    private sealed class FakeCredentials : IExternalCredentialProvider
    {
        public Task<Result<string>> GetApiKeyAsync(Guid userId, Guid accountId, CancellationToken ct = default) => Task.FromResult(Result<string>.Success("fake-key"));
        public Task<Result<ExternalAccessToken>> GetAccessTokenAsync(Guid userId, Guid accountId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Result<PasswordCredential>> GetPasswordAsync(Guid userId, Guid accountId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed record Envelope<T>(T Data);
}
