using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTaskListBindings;

public sealed record GetTaskListBindingsInput(Guid UserId);

/// <summary>The user's Google task list links, with their sync status.</summary>
[RequiresPermission(AgendaPermissions.Lists.Read)]
public sealed class GetTaskListBindingsQuery(GetTaskListBindingsInput input)
    : QueryBase<GetTaskListBindingsInput, IReadOnlyList<TaskListBindingDto>>(input);
