using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTaskLists;

public sealed class GetTaskListsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetTaskListsQuery, IReadOnlyList<TaskListDto>>
{
    protected override async Task<Result<IReadOnlyList<TaskListDto>>> HandleAsync(
        GetTaskListsQuery request, CancellationToken cancellationToken)
    {
        var lists = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, ct) =>
        {
            var repo = context.AcquireRepository<ITaskListRepository>();
            return await repo.GetByUserAsync(request.Input.UserId, ct);
        }, cancellationToken: cancellationToken);

        IReadOnlyList<TaskListDto> dtos = [.. lists.Select(l => l.ToDto())];
        return Ok(dtos);
    }
}
