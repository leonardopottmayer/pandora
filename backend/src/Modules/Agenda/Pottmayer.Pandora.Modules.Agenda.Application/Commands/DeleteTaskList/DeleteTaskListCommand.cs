using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteTaskList;

public sealed record DeleteTaskListInput(Guid UserId, Guid ListId);

/// <summary>Deletes an empty task list. Refused while it still has live tasks — archive instead.</summary>
[RequiresPermission(AgendaPermissions.Lists.Delete)]
public sealed class DeleteTaskListCommand(DeleteTaskListInput input)
    : CommandBase<DeleteTaskListInput, bool>(input);
