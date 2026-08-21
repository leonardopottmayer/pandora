using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateTask;

/// <summary>Edits a task's core fields. Status transitions go through complete/reopen, not here.</summary>
public sealed record UpdateTaskInput(
    Guid UserId,
    Guid TaskId,
    string Title,
    string? Notes,
    DateTimeOffset? DueAt,
    bool DueHasTime,
    TaskPriority Priority);

public sealed class UpdateTaskCommand(UpdateTaskInput input)
    : CommandBase<UpdateTaskInput, TaskDto>(input);
