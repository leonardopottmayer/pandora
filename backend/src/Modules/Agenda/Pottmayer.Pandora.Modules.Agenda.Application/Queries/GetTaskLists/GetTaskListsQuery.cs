using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Security.Authorization.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTaskLists;

public sealed record GetTaskListsInput(Guid UserId);

[RequiresPermission(AgendaPermissions.Lists.Read)]
public sealed class GetTaskListsQuery(GetTaskListsInput input)
    : QueryBase<GetTaskListsInput, IReadOnlyList<TaskListDto>>(input);
