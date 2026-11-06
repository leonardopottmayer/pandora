using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteTask;

public sealed record DeleteTaskInput(Guid UserId, Guid TaskId);

/// <summary>Soft-deletes a task.</summary>
[RequiresPermission(AgendaPermissions.Tasks.Delete)]
public sealed class DeleteTaskCommand(DeleteTaskInput input)
    : CommandBase<DeleteTaskInput, bool>(input);
