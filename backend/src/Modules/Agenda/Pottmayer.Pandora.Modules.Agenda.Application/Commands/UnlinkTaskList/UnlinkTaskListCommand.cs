using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.UnlinkTaskList;

public sealed record UnlinkTaskListInput(Guid UserId, Guid BindingId);

/// <summary>Stops syncing a Google task list. The Pandora list and its tasks stay; nothing is deleted in Google.</summary>
[RequiresPermission(AgendaPermissions.Lists.Delete)]
public sealed class UnlinkTaskListCommand(UnlinkTaskListInput input)
    : CommandBase<UnlinkTaskListInput, bool>(input);
