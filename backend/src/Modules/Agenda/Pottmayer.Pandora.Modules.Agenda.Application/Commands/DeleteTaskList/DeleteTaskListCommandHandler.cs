using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteTaskList;

public sealed class DeleteTaskListCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<DeleteTaskListCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(DeleteTaskListCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var outcome = await factory.ExecuteAsync(AgendaModule.Name, async (context, token) =>
        {
            var lists = context.AcquireRepository<ITaskListRepository>();
            var tasks = context.AcquireRepository<ITaskRepository>();

            var list = await lists.FindAsync(input.UserId, input.ListId, token);
            if (list is null)
                return Outcome.Missing;

            var live = await tasks.GetByUserAsync(input.UserId, input.ListId, null, token);
            if (live.Count > 0)
                return Outcome.NotEmpty;

            await lists.RemoveAsync(list, token);
            return Outcome.Deleted;
        }, cancellationToken: ct);

        return outcome switch
        {
            Outcome.Missing => Fail(TaskErrors.ListNotFound),
            Outcome.NotEmpty => Fail(TaskErrors.ListNotEmpty),
            _ => Ok(true),
        };
    }

    private enum Outcome { Missing, NotEmpty, Deleted }
}
