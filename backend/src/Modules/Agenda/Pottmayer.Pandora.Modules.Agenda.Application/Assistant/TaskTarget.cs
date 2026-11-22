using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTasks;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands.ToolArguments;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// The task a call is about, for the task tools: the one the user pointed at on a list (<c>ref</c>), or the
/// one among <paramref name="which"/> whose title matches their words (<c>task</c>).
/// </summary>
internal static class TaskTarget
{
    public const string SchemaProperties = """
            "ref": { "type": "integer", "description": "The task's number on the last list shown, when the user points at it by number." },
            "task": { "type": "string", "description": "The task as the user named it (e.g. \"aluguel\", \"renew passport\"), when not pointing by number." }
        """;

    public static async Task<(TaskDto? Match, string? Problem)> FindAsync(
        ISender sender, AssistantToolContext context, JsonElement arguments,
        Func<TaskDto, bool> which, string wherePt, string whereEn, CancellationToken ct)
    {
        var tasks = await sender.Send(new GetTasksQuery(new GetTasksInput(context.UserId, null, null, null)), ct);
        return PickTarget(context, arguments, "task", "task", (tasks.Value ?? []).Where(which),
            t => t.Id, t => t.Title, wherePt, whereEn);
    }

    /// <summary>A found task as the item a confirmation pins (<see cref="IAssistantTargetedTool"/>).</summary>
    public static (ListedItem? Target, string? Problem) AsTarget((TaskDto? Match, string? Problem) found) =>
        found.Match is { } task ? (new ListedItem("task", task.Id, task.Title), null) : (null, found.Problem);

    /// <summary>The task's title as the user will recognize it, for the confirmation question.</summary>
    public static string Name(JsonElement arguments) => TargetName(arguments, "task");
}
