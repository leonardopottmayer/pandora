using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CompleteTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTasks;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using static Pottmayer.Pandora.Modules.Agenda.Application.Assistant.ToolArguments;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Assistant;

/// <summary>
/// <c>complete_task</c>: marks an open task done. The model passes only the words the user said; the task
/// is found here among the user's open tasks (<see cref="ToolArguments.PickByTitle{T}"/>), so no task
/// title is ever sent to the model. Forwarded to <see cref="CompleteTaskCommand"/>.
/// </summary>
public sealed class CompleteTaskTool(ISender sender) : IAssistantTool
{
    public AssistantCommandDescriptor Descriptor { get; } = new(
        Name: "complete_task",
        Description: "Marks one of the user's open tasks as done. You cannot see the tasks: pass the words the user used to name it; the system finds it.",
        ParametersJsonSchema: """
        {
          "type": "object",
          "properties": {
            "task": { "type": "string", "description": "The task as the user named it (e.g. \"aluguel\", \"renew passport\")." }
          },
          "required": ["task"]
        }
        """,
        Confirmation: ConfirmationPolicy.WhenAmbiguous,
        Examples:
        [
            new AssistantCommandExample("mark renew passport as done", """{ "task": "renew passport" }"""),
            new AssistantCommandExample("I already bought the printer ink", """{ "task": "printer ink" }"""),
        ]);

    public string Describe(AssistantToolContext context, JsonElement arguments)
    {
        var query = RequiredString(arguments, "task");
        return context.Text($"Concluir a tarefa \"{query}\"?", $"Mark the task \"{query}\" as done?");
    }

    public async Task<AssistantCommandOutcome> ExecuteAsync(AssistantToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var query = RequiredString(arguments, "task");

        var tasks = await sender.Send(new GetTasksQuery(new GetTasksInput(context.UserId, null, null, null)), ct);
        var open = (tasks.Value ?? []).Where(t =>
            t.Status is nameof(TaskItemStatus.Todo) or nameof(TaskItemStatus.InProgress));

        var (task, problem) = PickByTitle(context, open, t => t.Title, query, "suas tarefas abertas", "your open tasks");
        if (task is null)
            return AssistantCommandOutcome.Failed(problem!);

        var result = await sender.Send(new CompleteTaskCommand(new CompleteTaskInput(context.UserId, task.Id)), ct);
        if (!result.IsSuccess)
            return AssistantCommandOutcome.Failed(string.Join("; ", result.Errors.Select(e => e.Message)));

        return AssistantCommandOutcome.Ok(context.Text(
            $"Tarefa \"{task.Title}\" concluída.",
            $"Task \"{task.Title}\" marked as done."));
    }
}
