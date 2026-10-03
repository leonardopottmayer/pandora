using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Notes.Application.Dtos;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPage;
using Pottmayer.Pandora.Modules.Notes.Application.Queries.GetPageTree;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Notes.Application.Assistant;

/// <summary>
/// The note a call is about, for <see cref="ReadNoteTool"/> and <see cref="AppendToNoteTool"/>: the one the
/// user pointed at on a search's list (<c>ref</c>), or the open page whose title matches their words
/// (<c>note</c>) — loaded whole, body included.
/// </summary>
internal static class NoteTarget
{
    public const string SchemaProperties = """
            "ref": { "type": "integer", "description": "The note's number on the last list shown, when the user points at it by number." },
            "note": { "type": "string", "description": "The note's title as the user said it, when not pointing by number." }
        """;

    public static async Task<(PageDto? Match, string? Problem)> FindAsync(
        ISender sender, AssistantToolContext context, JsonElement arguments, CancellationToken ct)
    {
        var pages = await sender.Send(new GetPageTreeQuery(new GetPageTreeInput(context.UserId, IncludeArchived: false)), ct);
        var (summary, problem) = PickTarget(context, arguments, "note", "note", pages.Value ?? [],
            p => p.Id, p => p.Title, "suas notas", "your notes");
        if (summary is null)
            return (null, problem);

        var page = await sender.Send(new GetPageQuery(new GetPageInput(context.UserId, summary.Id)), ct);
        return page.IsSuccess
            ? (page.Value, null)
            : (null, string.Join("; ", page.Errors.Select(e => e.Message)));
    }

    /// <summary>The note's title as the user will recognize it, for the confirmation question.</summary>
    public static string Name(JsonElement arguments) => TargetName(arguments, "note");
}
