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

        // Promoting a list to default: demote the current default first, in its own transaction, so
        // the partial unique index (one default per user) never sees two at once. Guarded by the
        // target existing, so a bad id does not leave the user with no default.
        if (input.IsDefault == true)
            await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
            {
                var lists = context.AcquireRepository<ITaskListRepository>();
                if (await lists.FindAsync(input.UserId, input.ListId, token) is null)
                    return false;

                var all = await lists.GetByUserAsync(input.UserId, token);
                foreach (var other in all.Where(l => l.IsDefault && l.Id != input.ListId))
                {
                    other.SetDefault(false);
                    await lists.UpdateAsync(other, token);
                }
                return true;
            }, cancellationToken: ct);

        var list = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
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
