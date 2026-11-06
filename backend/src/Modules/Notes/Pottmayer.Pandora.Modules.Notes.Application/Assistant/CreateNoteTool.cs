using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.CreatePage;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetSpaces;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;
using Pottmayer.Pandora.Modules.Notes.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Assistant;

/// <summary>
/// <c>create_note</c>: a new top-level page in Notes, forwarded to <see cref="CreatePageCommand"/>. The
/// body is the user's text as markdown, so #tags and [[links]] in it work as in the editor.
/// </summary>
public sealed class CreateNoteTool(ISender sender) : IAssistantTool
{
    public IReadOnlyList<string> Permissions { get; } = [NotesPermissions.Pages.Create, NotesPermissions.Spaces.Read];

    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "create_note",
        Description: "Saves a note (an idea, a piece of information to keep). Use when the user wants to write something down, not to be pinged (create_reminder) or to do it (create_task).",
        ParametersJsonSchema: """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "A short title for the note, in the user's language." },
            "content": { "type": "string", "description": "The note itself, as the user said it (markdown allowed). Omit when the title says it all." },
            "space": { "type": "string", "description": "The space to put it in, by name, only when the user names one. Omit to use their default space." }
          },
          "required": ["title"]
        }
        """,
        Confirmation: ConfirmationPolicy.WhenAmbiguous,
        Examples:
        [
            new AssistantCommandExample(
                "note: the wifi password at mom's is girassol2024",
                """{ "title": "Wifi at mom's", "content": "Password: girassol2024" }"""),
            new AssistantCommandExample(
                "write down an idea: an app that splits the grocery bill among roommates",
                """{ "title": "Grocery bill splitting app", "content": "An app that splits the grocery bill among roommates." }"""),
            new AssistantCommandExample(
                "in the Work space, note that the deploy window is on Thursdays",
                """{ "title": "Deploy window", "content": "Deploys happen on Thursdays.", "space": "Work" }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var (title, _, space) = Parse(arguments);
        return space is null
            ? context.Text($"Criar a nota \"{title}\"?", $"Create the note \"{title}\"?")
            : context.Text($"Criar a nota \"{title}\" no espaço {space}?", $"Create the note \"{title}\" in the {space} space?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var (title, content, spaceName) = Parse(arguments);

        Guid? spaceId = null;
        if (spaceName is not null)
        {
            var spaces = (await sender.Send(new GetSpacesQuery(new GetSpacesInput(context.UserId, IncludeArchived: false)), ct)).Value ?? [];
            // The name as said first, then a space whose name contains it ("trabalho" for "Trabalho - Acme").
            var match = spaces.FirstOrDefault(s => s.Name.Equals(spaceName, StringComparison.OrdinalIgnoreCase))
                ?? spaces.FirstOrDefault(s => s.Name.Contains(spaceName, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                var names = string.Join(", ", spaces.Select(s => s.Name));
                return AssistantCommandOutcome.Failed(context.Text(
                    $"Não achei o espaço \"{spaceName}\". Seus espaços: {names}.",
                    $"No space called \"{spaceName}\". Your spaces: {names}."));
            }
            spaceId = match.Id;
        }

        var result = await sender.Send(new CreatePageCommand(new CreatePageInput(
            context.UserId, title, ParentId: null, Icon: null, content, spaceId, Source: PageVersionSource.Assistant)), ct);

        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        return AssistantCommandOutcome.Ok(context.Text(
            $"Nota \"{result.Value!.Title}\" criada no Notes.",
            $"Note \"{result.Value!.Title}\" created in Notes."));
    }

    private static (string Title, string? Content, string? Space) Parse(JsonElement arguments) =>
        (RequiredString(arguments, "title"), OptionalString(arguments, "content"), OptionalString(arguments, "space"));
}
