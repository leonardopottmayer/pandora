using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.UnlinkTaskList;

public sealed class UnlinkTaskListCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<UnlinkTaskListCommand, bool>
{
    protected override async Task<Result<bool>> HandleAsync(UnlinkTaskListCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var found = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var binding = await context.AcquireRepository<ITaskListBindingRepository>().FindAsync(input.UserId, input.BindingId, token);
            if (binding is null)
                return false;

            await TaskListUnlinking.UnlinkAsync(context, binding, token);
            return true;
        }, cancellationToken: ct);

        return found ? Ok(true) : Fail(GoogleTasksErrors.BindingNotFound);
    }
}

/// <summary>Removes a binding and its links; the list and its tasks stay, as an ordinary list.</summary>
internal static class TaskListUnlinking
{
    public static async Task UnlinkAsync(IDataContext context, TaskListBinding binding, CancellationToken ct)
    {
        var links = context.AcquireRepository<ITaskLinkRepository>();
        foreach (var link in await links.GetByBindingAsync(binding.Id, ct))
            await links.RemoveAsync(link, ct);

        await context.AcquireRepository<ITaskListBindingRepository>().RemoveAsync(binding, ct);
    }
}
