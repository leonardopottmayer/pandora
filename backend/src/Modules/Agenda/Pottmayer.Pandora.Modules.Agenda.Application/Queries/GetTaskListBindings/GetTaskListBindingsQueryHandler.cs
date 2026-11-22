using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTaskListBindings;

public sealed class GetTaskListBindingsQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetTaskListBindingsQuery, IReadOnlyList<TaskListBindingDto>>
{
    protected override async Task<Result<IReadOnlyList<TaskListBindingDto>>> HandleAsync(
        GetTaskListBindingsQuery request, CancellationToken cancellationToken)
    {
        var userId = request.Input.UserId;
        var dtos = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, ct) =>
        {
            var bindings = await context.AcquireRepository<ITaskListBindingRepository>().GetByUserAsync(userId, ct);
            var lists = (await context.AcquireRepository<ITaskListRepository>().GetByUserAsync(userId, ct))
                .ToDictionary(l => l.Id);

            IReadOnlyList<TaskListBindingDto> list =
                [.. bindings.Where(b => lists.ContainsKey(b.ListId)).Select(b => b.ToDto(lists[b.ListId]))];
            return list;
        }, cancellationToken: cancellationToken);

        return Ok(dtos);
    }
}
