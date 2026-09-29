using System.Text;
using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.SearchPages;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Notes.Application.Assistant;

/// <summary>
/// <c>search_notes</c>: full-text search over the user's open pages (<see cref="SearchPagesQuery"/>),
/// each hit shown with its excerpt around the match. Like <c>list_agenda</c>, the list goes straight to
/// the user and the conversation history keeps only a content-free recap, so no note reaches the model.
/// </summary>
public sealed class SearchNotesTool(ISender sender) : IAssistantTool
{
    /// <summary>A chat reply stays short; the palette in the web app shows the full search.</summary>
    private const int MaxShown = 5;

    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "search_notes",
        Description: "Searches the user's notes and shows them the matching ones with an excerpt. Use when they look for something they wrote down. The results go to the user directly; you will not see them.",
        ParametersJsonSchema: """
        {
          "type": "object",
          "properties": {
            "query": { "type": "string", "description": "The words to search for, as the user said them (e.g. \"wifi\", \"senha do wifi\")." }
          },
          "required": ["query"]
        }
        """,
        Confirmation: ConfirmationPolicy.Never,
        Examples:
        [
            new AssistantCommandExample(
                "what did I write down about the wifi at mom's?",
                """{ "query": "wifi mom" }"""),
            new AssistantCommandExample(
                "find my notes on the grocery app idea",
                """{ "query": "grocery app" }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var query = Parse(arguments);
        return context.Text($"Buscar \"{query}\" nas suas notas?", $"Search your notes for \"{query}\"?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var query = Parse(arguments);

        var result = await sender.Send(new SearchPagesQuery(new SearchPagesInput(context.UserId, query)), ct);
        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        var hits = (result.Value ?? []).Where(p => !p.IsArchived).ToList();
        var recap = $"[search_notes: {hits.Count} note(s) shown to the user; content withheld from you]";

        if (hits.Count == 0)
            return AssistantCommandOutcome.Ok(context.Text(
                $"Nenhuma nota encontrada para \"{query}\".", $"No notes found for \"{query}\"."), recap);

        var sb = new StringBuilder(context.Text($"Notas para \"{query}\":", $"Notes for \"{query}\":"));
        foreach (var hit in hits.Take(MaxShown))
        {
            sb.Append("\n• ").Append(hit.Title);
            if (hit.Excerpt.Length > 0)
                sb.Append(" — ").Append(hit.Excerpt);
        }
        if (hits.Count > MaxShown)
            sb.Append('\n').Append(context.Text(
                $"…e mais {hits.Count - MaxShown}. A busca completa está no Notes.",
                $"…and {hits.Count - MaxShown} more. The full search is in Notes."));

        return AssistantCommandOutcome.Ok(sb.ToString(), recap);
    }

    private static string Parse(JsonElement arguments) => RequiredString(arguments, "query");
}
