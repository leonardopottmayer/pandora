using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Notes.Application.Assistant;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.CreatePage;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetSpaces;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Mediator.Abstractions.Messaging;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notes.Tests;

public sealed class CreateNoteToolTests
{
    private static readonly AssistantToolContext Context = new(Guid.NewGuid(), "pt-BR", TimeZoneInfo.Utc);

    private static readonly SpaceDto Work = new(
        Guid.NewGuid(), "Trabalho - Acme", null, null, null, IsDefault: false, IsArchived: false, 0, null, DateTimeOffset.UtcNow);

    /// <summary>Echoes the page it is asked to create, and knows one space besides the default.</summary>
    private sealed class EchoSender : ISender
    {
        public List<CreatePageInput> Created { get; } = [];

        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is GetSpacesQuery)
            {
                object spaces = Result<IReadOnlyList<SpaceDto>>.Success([Work]);
                return ValueTask.FromResult((TResponse)spaces);
            }

            var input = ((CreatePageCommand)(object)request).Input;
            Created.Add(input);
            object page = Result<PageDto>.Success(new PageDto(
                Guid.NewGuid(), input.SpaceId ?? Guid.NewGuid(), input.ParentId, input.FolderId, input.Title, "slug", JsonDocument.Parse(input.Content ?? PageContent.Empty.Json).RootElement.Clone(), input.Icon,
                0, false, false, DateTimeOffset.UtcNow, null, [], PageAppearance.Default, false, null, null, null));
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
        Assert.Equal("Senha: girassol2024", PageContent.Parse(input.Content).Text);
        Assert.Equal("Nota \"Wifi da mãe\" criada no Notes.", outcome.Message);
    }

    [Fact]
    public async Task Puts_the_note_in_the_space_the_user_named()
    {
        var sender = new EchoSender();

        await new CreateNoteTool(sender).ExecuteAsync(
            Context, Args("""{ "title": "Janela de deploy", "space": "trabalho" }"""));

        Assert.Equal(Work.Id, Assert.Single(sender.Created).SpaceId);
    }

    [Fact]
    public async Task Leaves_the_space_to_the_default_when_none_is_named()
    {
        var sender = new EchoSender();

        await new CreateNoteTool(sender).ExecuteAsync(Context, Args("""{ "title": "Ideia" }"""));

        Assert.Null(Assert.Single(sender.Created).SpaceId);
    }

    [Fact]
    public async Task Names_the_spaces_there_are_when_the_one_asked_for_is_not_one()
    {
        var sender = new EchoSender();

        var outcome = await new CreateNoteTool(sender).ExecuteAsync(
            Context, Args("""{ "title": "Ideia", "space": "Viagens" }"""));

        Assert.Empty(sender.Created);
        Assert.False(outcome.Success);
        Assert.Contains("Trabalho - Acme", outcome.Message);
    }

    [Fact]
    public void A_note_needs_a_title() =>
        Assert.Throws<ArgumentException>(() =>
            new CreateNoteTool(new EchoSender()).Describe(Context, Args("""{ "content": "x" }""")));
}
