using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.BindGoogleTaskLists;

/// <summary>The Google task lists of <see cref="AccountId"/> to sync, each as its own Pandora list.</summary>
public sealed record BindGoogleTaskListsInput(Guid UserId, Guid AccountId, IReadOnlyList<string> RemoteListIds);

/// <summary>
/// Creates one Pandora task list per Google task list (named like it) and the binding that syncs it. A list
/// already bound is left as it is. The tasks arrive on the first sync.
/// </summary>
[RequiresPermission(AgendaPermissions.Lists.Create)]
public sealed class BindGoogleTaskListsCommand(BindGoogleTaskListsInput input)
    : CommandBase<BindGoogleTaskListsInput, IReadOnlyList<TaskListBindingDto>>(input);
