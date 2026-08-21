using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Tars.Core.Cqrs.Queries;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTaskLists;

public sealed record GetTaskListsInput(Guid UserId);

public sealed class GetTaskListsQuery(GetTaskListsInput input)
    : QueryBase<GetTaskListsInput, IReadOnlyList<TaskListDto>>(input);
