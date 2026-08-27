using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateTaskList;

public sealed class CreateTaskListCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<CreateTaskListCommand, TaskListDto>
{
    protected override async Task<Result<TaskListDto>> HandleAsync(CreateTaskListCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (string.IsNullOrWhiteSpace(input.Name))
            return Fail(TaskErrors.TitleRequired);

        var created = TaskList.Create(input.UserId, input.Name, input.IsDefault, input.Position, timeProvider);

        await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            await context.AcquireRepository<ITaskListRepository>().AddAsync(created, token);
            return true;
        }, cancellationToken: ct);

        return Ok(created.ToDto());
    }
}
