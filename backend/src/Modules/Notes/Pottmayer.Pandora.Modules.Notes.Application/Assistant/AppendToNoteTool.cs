using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.UpdatePage;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Notes.Application.Assistant;

/// <summary>
/// <c>append_to_note</c>: adds the user's text at the end of a note, after a blank line
/// (<see cref="UpdatePageCommand"/>). Only appending: editing the middle of a note would mean sending it to
/// the model, which never sees the user's notes.
/// </summary>
public sealed class AppendToNoteTool(ISender sender) : IAssistantTargetedTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "append_to_note",
        Description: "Adds text at the end of one of the user's existing notes. You cannot see the notes: point at it by its number on the last list, or pass the title the user said. To start a new note use create_note.",
        ParametersJsonSchema: $$"""
        {
          "type": "object",
          "properties": {
            {{NoteTarget.SchemaProperties}},
            "text": { "type": "string", "description": "What to add, as the user said it (markdown allowed)." }
          },
          "required": ["text"]
        }
        """,
        Confirmation: ConfirmationPolicy.WhenAmbiguous,
        Examples:
        [
            new AssistantCommandExample(
                "add to the grocery app note: it could also split rent",
                """{ "note": "grocery app", "text": "It could also split rent." }"""),
            new AssistantCommandExample(
                "append to 1: call the landlord on Monday",
                """{ "ref": 1, "text": "Call the landlord on Monday." }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var name = NoteTarget.Name(arguments);
        var text = RequiredString(arguments, "text");
        return context.Text($"Acrescentar à nota \"{name}\": \"{text}\"?", $"Add to the note \"{name}\": \"{text}\"?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var text = RequiredString(arguments, "text");

        var (page, problem) = await NoteTarget.FindAsync(sender, context, arguments, ct);
        if (page is null)
            return AssistantCommandOutcome.Failed(problem!);

        var body = page.ContentMarkdown.TrimEnd();
        var content = body.Length == 0 ? text : $"{body}\n\n{text}";

        var result = await sender.Send(new UpdatePageCommand(new UpdatePageInput(
            context.UserId, page.Id, page.Title, page.Icon, content)), ct);
        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        return AssistantCommandOutcome.Ok(context.Text(
            $"Texto acrescentado à nota \"{page.Title}\".",
            $"Text added to the note \"{page.Title}\"."));
    }

    public async Task<(ListedItem? Target, string? Problem)> FindTargetAsync(
        AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var (page, problem) = await NoteTarget.FindAsync(sender, context, arguments, ct);
        return page is null ? (null, problem) : (new ListedItem("note", page.Id, page.Title), null);
    }
}
