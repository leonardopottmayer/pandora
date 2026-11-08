using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Notes.Application.Assistant;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.AskNotes;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Mediator.Abstractions.Messaging;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class AskNotesToolTests
{
    private static readonly AssistantToolContext Context = new(Guid.NewGuid(), "pt-BR", TimeZoneInfo.Utc);

    /// <summary>Answers every question with the given answer and records what was asked.</summary>
    private sealed class AnswerSender(NoteAnswerDto answer) : ISender
    {
        public List<AskNotesInput> Asked { get; } = [];

        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Asked.Add(((AskNotesCommand)(object)request).Input);
            object result = Result<NoteAnswerDto>.Success(answer);
            return ValueTask.FromResult((TResponse)result);
        }
    }

    private static NoteSourceDto Source(string title) => new(Guid.NewGuid(), Guid.NewGuid(), title, null, "…");

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public async Task Answers_with_the_cited_notes_numbered_and_keeps_the_answer_for_follow_ups()
    {
        var trip = Source("Roteiro Floripa");
        var sender = new AnswerSender(new NoteAnswerDto("Sábado em Jurerê, domingo na Lagoa.", [trip]));

        var outcome = await new AskNotesTool(sender).ExecuteAsync(Context,
            Args("""{ "question": "Qual o roteiro de Floripa?", "search_terms": "floripa roteiro viagem" }"""));

        var input = Assert.Single(sender.Asked);
        Assert.Equal(Context.UserId, input.UserId);
        Assert.Equal("Qual o roteiro de Floripa?", input.Question);
        Assert.Equal("floripa roteiro viagem", input.SearchTerms);
        Assert.Equal("Sábado em Jurerê, domingo na Lagoa.\n\nFonte:\n1. Roteiro Floripa", outcome.Message);
        Assert.Equal(trip.PageId, Assert.Single(outcome.Listed!).Id);
        Assert.Contains("Jurerê", outcome.Recap);
    }

    [Fact]
    public async Task Says_so_when_the_notes_do_not_answer()
    {
        var outcome = await new AskNotesTool(new AnswerSender(new NoteAnswerDto(null, [])))
            .ExecuteAsync(Context, Args("""{ "question": "Qual a placa do carro?" }"""));

        Assert.True(outcome.Success);
        Assert.Equal("Não encontrei isso nas suas notas.", outcome.Message);
        Assert.Null(outcome.Listed);
    }
}
