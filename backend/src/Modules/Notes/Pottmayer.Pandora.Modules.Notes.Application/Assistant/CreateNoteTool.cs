using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.CreatePage;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Notes.Application.Assistant;

/// <summary>
/// <c>create_note</c>: a new top-level page in Notes, forwarded to <see cref="CreatePageCommand"/>. The
/// body is the user's text as markdown, so #tags and [[links]] in it work as in the editor.
/// </summary>
public sealed class CreateNoteTool(ISender sender) : IAssistantTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "create_note",
        Description: "Saves a note (an idea, a piece of information to keep). Use when the user wants to write something down, not to be pinged (create_reminder) or to do it (create_task).",
        ParametersJsonSchema: """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "A short title for the note, in the user's language." },
            "content": { "type": "string", "description": "The note itself, as the user said it (markdown allowed). Omit when the title says it all." }
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
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var (title, _) = Parse(arguments);
        return context.Text($"Criar a nota \"{title}\"?", $"Create the note \"{title}\"?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var (title, content) = Parse(arguments);

        var result = await sender.Send(new CreatePageCommand(new CreatePageInput(
            context.UserId, title, ParentId: null, Icon: null, content)), ct);

        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        return AssistantCommandOutcome.Ok(context.Text(
            $"Nota \"{result.Value!.Title}\" criada no Notes.",
            $"Note \"{result.Value!.Title}\" created in Notes."));
    }

    private static (string Title, string? Content) Parse(JsonElement arguments) =>
        (RequiredString(arguments, "title"), OptionalString(arguments, "content"));
}
