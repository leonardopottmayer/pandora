using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.CompleteTask;

public sealed record CompleteTaskInput(Guid UserId, Guid TaskId);

/// <summary>
/// Completes a task. A recurring task also spawns its next instance (carrying notes, priority, list,
/// rule and alerts). Idempotent: completing an already-done task spawns nothing.
/// </summary>
public sealed class CompleteTaskCommand(CompleteTaskInput input)
    : CommandBase<CompleteTaskInput, TaskDto>(input);
