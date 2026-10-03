using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;

namespace Pottmayer.Pandora.Modules.Notes.Application.Assistant;

/// <summary>
/// <c>read_note</c>: sends the user one note whole — its title and markdown body, as written. The body goes
/// straight to the user (a long one in several messages); the history keeps a content-free recap.
/// </summary>
public sealed class ReadNoteTool(ISender sender) : IAssistantTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "read_note",
        Description: "Shows the user the full content of one of their notes. You cannot see the notes: point at it by its number on the last list, or pass the title the user said. The content goes to the user directly; you will not see it.",
        ParametersJsonSchema: $$"""
        {
          "type": "object",
          "properties": {
            {{NoteTarget.SchemaProperties}}
          }
        }
        """,
        Confirmation: ConfirmationPolicy.Never,
        Examples:
        [
            new AssistantCommandExample("open the note about the wifi at mom's", """{ "note": "wifi mom" }"""),
            new AssistantCommandExample("show me 2", """{ "ref": 2 }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var name = NoteTarget.Name(arguments);
        return context.Text($"Mostrar a nota \"{name}\"?", $"Show the note \"{name}\"?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var (page, problem) = await NoteTarget.FindAsync(sender, context, arguments, ct);
        if (page is null)
            return AssistantCommandOutcome.Failed(problem!);

        var body = page.ContentMarkdown.Trim();
        return AssistantCommandOutcome.Ok(
            body.Length == 0
                ? context.Text($"{page.Title}\n\n(nota vazia)", $"{page.Title}\n\n(empty note)")
                : $"{page.Title}\n\n{body}",
            "[read_note: one note shown to the user; content withheld from you]");
    }
}
