using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateTaskList;

public sealed class UpdateTaskListCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<UpdateTaskListCommand, TaskListDto>
{
    protected override async Task<Result<TaskListDto>> HandleAsync(UpdateTaskListCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (input.Name is not null && string.IsNullOrWhiteSpace(input.Name))
            return Fail(TaskErrors.TitleRequired);

        var list = await factory.ExecuteAsync(AgendaModule.Name, async (context, token) =>
        {
            var lists = context.AcquireRepository<ITaskListRepository>();
            var found = await lists.FindAsync(input.UserId, input.ListId, token);
            if (found is null)
                return null;

            if (input.Name is not null)
                found.Rename(input.Name);
            if (input.Position is { } position)
                found.SetPosition(position);
            if (input.IsDefault is { } isDefault)
                found.SetDefault(isDefault);
            if (input.Archive)
                found.Archive(timeProvider);

            await lists.UpdateAsync(found, token);
            return found;
        }, cancellationToken: ct);

        return list is null ? Fail(TaskErrors.ListNotFound) : Ok(list.ToDto());
    }
}
