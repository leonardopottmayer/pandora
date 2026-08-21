using Pottmayer.Tars.Core.Cqrs.Commands;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteTaskList;

public sealed record DeleteTaskListInput(Guid UserId, Guid ListId);

/// <summary>Deletes an empty task list. Refused while it still has live tasks — archive instead.</summary>
public sealed class DeleteTaskListCommand(DeleteTaskListInput input)
    : CommandBase<DeleteTaskListInput, bool>(input);
