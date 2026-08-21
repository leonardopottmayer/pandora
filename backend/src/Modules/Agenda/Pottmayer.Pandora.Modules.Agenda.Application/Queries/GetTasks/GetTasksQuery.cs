using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTasks;

/// <summary>A coarse due-date bucket for the task list, computed against the UTC day (per-user zone deferred).</summary>
public enum TaskDueBucket { Overdue, Today, Week, Later, None }

public sealed record GetTasksInput(Guid UserId, Guid? ListId, TaskItemStatus? Status, TaskDueBucket? Due);

public sealed class GetTasksQuery(GetTasksInput input)
    : QueryBase<GetTasksInput, IReadOnlyList<TaskDto>>(input);
