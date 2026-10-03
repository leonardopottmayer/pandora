using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Notes.Application.Assistant;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.UpdatePage;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Mediator.Abstractions.Messaging;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

/// <summary><c>read_note</c> and <c>append_to_note</c>.</summary>
public sealed class NoteAssistantToolsTests
{
    private static readonly AssistantToolContext Context = new(Guid.NewGuid(), "pt-BR", TimeZoneInfo.Utc);

    /// <summary>Answers each request with the scripted response of its type, and records what was sent.</summary>
    private sealed class ScriptedSender(params object[] responses) : ISender
    {
        public List<object> Sent { get; } = [];

        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return ValueTask.FromResult(responses.OfType<TResponse>().First());
        }
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static PageDto Page(string title, string content) =>
        new(Guid.NewGuid(), null, title, "slug", content, "💡", 0, false, false, DateTimeOffset.UtcNow, null, []);

    private static ScriptedSender Sending(PageDto page, params PageDto[] others) => new(
        Result<IReadOnlyList<PageSummaryDto>>.Success(
            [.. others.Prepend(page).Select(p => new PageSummaryDto(p.Id, null, p.Title, p.Slug, p.Icon, 0, false, false))]),
        Result<PageDto>.Success(page));

    [Fact]
    public async Task Read_note_sends_the_body_to_the_user_and_keeps_it_out_of_the_recap()
    {
        var page = Page("Wifi da mãe", "Senha: girassol2024\n");
        var sender = Sending(page, Page("Roteador", ""));

        var outcome = await new ReadNoteTool(sender).ExecuteAsync(Context, Args("""{ "note": "wifi" }"""));

        Assert.Equal("Wifi da mãe\n\nSenha: girassol2024", outcome.Message);
        Assert.DoesNotContain("girassol", outcome.Recap);
    }

    [Fact]
    public async Task Append_adds_the_text_after_a_blank_line_keeping_title_and_icon()
    {
        var page = Page("Ideia: app de mercado", "Divide a conta entre colegas.\n\n");
        var sender = Sending(page);
        var arguments = ListedRefs.Pin(Args("""{ "ref": 1, "text": "Também dividir o aluguel." }"""),
            [new ListedItem("note", page.Id, page.Title)]);

        var outcome = await new AppendToNoteTool(sender).ExecuteAsync(Context, arguments);

        var input = sender.Sent.OfType<UpdatePageCommand>().Single().Input;
        Assert.Equal((page.Id, page.Title, page.Icon), (input.PageId, input.Title, input.Icon));
        Assert.Equal("Divide a conta entre colegas.\n\nTambém dividir o aluguel.", input.ContentMarkdown);
        Assert.Equal("Texto acrescentado à nota \"Ideia: app de mercado\".", outcome.Message);
    }

    [Fact]
    public async Task Append_to_an_empty_note_writes_just_the_text()
    {
        var page = Page("Vazia", "");
        var sender = Sending(page);

        await new AppendToNoteTool(sender).ExecuteAsync(Context, Args("""{ "note": "vazia", "text": "Primeira linha." }"""));

        Assert.Equal("Primeira linha.", sender.Sent.OfType<UpdatePageCommand>().Single().Input.ContentMarkdown);
    }
}
