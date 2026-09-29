using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Notes.Application.Assistant;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.SearchPages;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Mediator.Abstractions.Messaging;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class SearchNotesToolTests
{
    private static readonly AssistantToolContext Context = new(Guid.NewGuid(), "pt-BR", TimeZoneInfo.Utc);

    /// <summary>Answers every search with the given hits and records what was searched.</summary>
    private sealed class HitsSender(params PageSearchResultDto[] hits) : ISender
    {
        public List<SearchPagesInput> Searched { get; } = [];

        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Searched.Add(((SearchPagesQuery)(object)request).Input);
            object result = Result<IReadOnlyList<PageSearchResultDto>>.Success(hits);
            return ValueTask.FromResult((TResponse)result);
        }
    }

    private static PageSearchResultDto Hit(string title, string excerpt = "", bool archived = false) =>
        new(Guid.NewGuid(), title, "slug", null, archived, excerpt);

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public async Task Shows_the_open_hits_with_their_excerpt_and_keeps_them_out_of_the_recap()
    {
        var sender = new HitsSender(
            Hit("Wifi da mãe", "Senha: girassol2024"),
            Hit("Wifi antigo", "Senha velha", archived: true),
            Hit("Roteador"));

        var outcome = await new SearchNotesTool(sender).ExecuteAsync(Context, Args("""{ "query": "wifi" }"""));

        var input = Assert.Single(sender.Searched);
        Assert.Equal(Context.UserId, input.UserId);
        Assert.Equal("wifi", input.Term);
        Assert.Equal("Notas para \"wifi\":\n• Wifi da mãe — Senha: girassol2024\n• Roteador", outcome.Message);
        Assert.DoesNotContain("girassol", outcome.Recap);
        Assert.Contains("2 note(s)", outcome.Recap);
    }

    [Fact]
    public async Task Shows_five_and_says_how_many_more()
    {
        var sender = new HitsSender([.. Enumerable.Range(1, 7).Select(i => Hit($"Nota {i}"))]);

        var outcome = await new SearchNotesTool(sender).ExecuteAsync(Context, Args("""{ "query": "nota" }"""));

        Assert.Equal(5, outcome.Message.Split('\n').Count(l => l.StartsWith('•')));
        Assert.EndsWith("…e mais 2. A busca completa está no Notes.", outcome.Message);
    }

    [Fact]
    public async Task Says_so_when_nothing_matches()
    {
        var outcome = await new SearchNotesTool(new HitsSender()).ExecuteAsync(Context, Args("""{ "query": "xyz" }"""));

        Assert.True(outcome.Success);
        Assert.Equal("Nenhuma nota encontrada para \"xyz\".", outcome.Message);
        Assert.NotNull(outcome.Recap);
    }
}
