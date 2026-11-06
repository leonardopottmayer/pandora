using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.ReopenTask;

public sealed record ReopenTaskInput(Guid UserId, Guid TaskId);

/// <summary>Reopens a task, clearing its completion stamp.</summary>
[RequiresPermission(AgendaPermissions.Tasks.Update)]
public sealed class ReopenTaskCommand(ReopenTaskInput input)
    : CommandBase<ReopenTaskInput, TaskDto>(input);
