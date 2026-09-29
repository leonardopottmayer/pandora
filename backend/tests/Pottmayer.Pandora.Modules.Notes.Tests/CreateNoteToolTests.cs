using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Notes.Application.Assistant;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.CreatePage;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Mediator.Abstractions.Messaging;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class CreateNoteToolTests
{
    private static readonly AssistantToolContext Context = new(Guid.NewGuid(), "pt-BR", TimeZoneInfo.Utc);

    /// <summary>Echoes the page it is asked to create.</summary>
    private sealed class EchoSender : ISender
    {
        public List<CreatePageInput> Created { get; } = [];

        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            var input = ((CreatePageCommand)(object)request).Input;
            Created.Add(input);
            object page = Result<PageDto>.Success(new PageDto(
                Guid.NewGuid(), input.ParentId, input.Title, "slug", input.ContentMarkdown ?? "", input.Icon,
                0, false, false, DateTimeOffset.UtcNow, null, []));
            return ValueTask.FromResult((TResponse)page);
        }
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public async Task Creates_a_top_level_page_with_the_users_text()
    {
        var sender = new EchoSender();

        var outcome = await new CreateNoteTool(sender).ExecuteAsync(
            Context, Args("""{ "title": "Wifi da mãe", "content": "Senha: girassol2024" }"""));

        var input = Assert.Single(sender.Created);
        Assert.Equal(Context.UserId, input.UserId);
        Assert.Null(input.ParentId);
        Assert.Equal("Senha: girassol2024", input.ContentMarkdown);
        Assert.Equal("Nota \"Wifi da mãe\" criada no Notes.", outcome.Message);
    }

    [Fact]
    public void A_note_needs_a_title() =>
        Assert.Throws<ArgumentException>(() =>
            new CreateNoteTool(new EchoSender()).Describe(Context, Args("""{ "content": "x" }""")));
}
