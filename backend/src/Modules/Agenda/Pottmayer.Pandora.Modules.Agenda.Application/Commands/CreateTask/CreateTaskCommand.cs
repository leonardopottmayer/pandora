using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateTask;

/// <summary>
/// Creates a task. With <see cref="ParentTaskId"/> set it is a subtask (inheriting the parent's list);
/// otherwise it is a top-level task in <see cref="ListId"/> and may recur (<see cref="Rrule"/>).
/// </summary>
public sealed record CreateTaskInput(
    Guid UserId,
    Guid ListId,
    Guid? ParentTaskId,
    string Title,
    string? Notes,
    DateTimeOffset? DueAt,
    bool DueHasTime,
    TaskPriority Priority,
    string? TimeZone,
    string? Rrule,
    int Position = 0);

[RequiresPermission(AgendaPermissions.Tasks.Create)]
public sealed class CreateTaskCommand(CreateTaskInput input)
    : CommandBase<CreateTaskInput, TaskDto>(input);
