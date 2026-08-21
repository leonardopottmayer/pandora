using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteTask;

public sealed record DeleteTaskInput(Guid UserId, Guid TaskId);

/// <summary>Soft-deletes a task.</summary>
public sealed class DeleteTaskCommand(DeleteTaskInput input)
    : CommandBase<DeleteTaskInput, bool>(input);
