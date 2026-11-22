using System.Text;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Modules.Notes.Application.Commands.AskNotes;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Notes.Application.Assistant;

/// <summary>
/// <c>ask_notes</c>: answers a question from the user's notes (<see cref="AskNotesCommand"/>). Unlike
/// <c>search_notes</c>, passages of the best-matching notes go to the model that writes the answer, and
/// the answer stays in the history so a follow-up question can build on it. The cited notes are listed
/// by number, so "open 1" works next.
/// </summary>
public sealed class AskNotesTool(ISender sender) : IAssistantTool
{
    public IReadOnlyList<string> Permissions { get; } = [NotesPermissions.Pages.Read];

    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "ask_notes",
        Description: "Answers a question from what the user wrote in their notes (a trip plan, a password, a recipe step, a date). " +
                     "Use it when they ask something whose answer may be in their notes; use search_notes only when they want to find or list notes.",
        ParametersJsonSchema: """
        {
          "type": "object",
          "properties": {
            "question": { "type": "string", "description": "The question, as the user asked it." },
            "search_terms": { "type": "string", "description": "Other words the note may use, beyond the question's own: synonyms, places, names (e.g. for \"my next trip\": \"viagem roteiro passagem hotel voo\")." }
          },
          "required": ["question"]
        }
        """,
        Confirmation: ConfirmationPolicy.Never,
        Examples:
        [
            new AssistantCommandExample(
                "what's the plan for the Floripa trip?",
                """{ "question": "what's the plan for the Floripa trip?", "search_terms": "floripa florianópolis roteiro viagem" }"""),
            new AssistantCommandExample(
                "qual a senha do wifi da casa da minha mãe?",
                """{ "question": "qual a senha do wifi da casa da minha mãe?", "search_terms": "wifi senha rede mãe" }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var question = RequiredString(arguments, "question");
        return context.Text($"Procurar a resposta para \"{question}\" nas suas notas?", $"Look for the answer to \"{question}\" in your notes?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var question = RequiredString(arguments, "question");
        var terms = OptionalString(arguments, "search_terms");

        var result = await sender.Send(new AskNotesCommand(new AskNotesInput(context.UserId, question, SearchTerms: terms)), ct);
        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        var answer = result.Value!;
        if (answer.Answer is null || answer.Sources.Count == 0)
            return AssistantCommandOutcome.Ok(
                context.Text("Não encontrei isso nas suas notas.", "I couldn't find that in your notes."),
                "[ask_notes: no answer found in the notes]");

        var sb = new StringBuilder(answer.Answer).Append("\n\n").Append(context.Text("Fonte:", "Source:"));
        var listed = new List<ListedItem>();
        foreach (var source in answer.Sources)
        {
            listed.Add(new ListedItem("note", source.PageId, source.Title));
            sb.Append('\n').Append(listed.Count).Append(". ").Append(source.Title);
        }
        return AssistantCommandOutcome.Ok(sb.ToString(), $"[ask_notes: answered \"{answer.Answer}\" from {listed.Count} note(s)]", listed);
    }
}
