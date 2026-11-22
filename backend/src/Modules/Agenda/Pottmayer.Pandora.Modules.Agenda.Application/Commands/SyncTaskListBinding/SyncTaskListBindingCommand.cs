using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.SyncTaskListBinding;

public sealed record SyncTaskListBindingInput(Guid BindingId);

/// <summary>One sync cycle of a task list binding, run by the background job (no user behind it). Returns whether it ran.</summary>
public sealed class SyncTaskListBindingCommand(SyncTaskListBindingInput input)
    : CommandBase<SyncTaskListBindingInput, bool>(input);

public sealed record SyncTaskListBindingNowInput(Guid UserId, Guid BindingId);

/// <summary>"Sync now": resumes a stopped binding and runs a cycle at once. Returns the binding afterwards.</summary>
[RequiresPermission(AgendaPermissions.Lists.Update)]
public sealed class SyncTaskListBindingNowCommand(SyncTaskListBindingNowInput input)
    : CommandBase<SyncTaskListBindingNowInput, TaskListBindingDto>(input);
