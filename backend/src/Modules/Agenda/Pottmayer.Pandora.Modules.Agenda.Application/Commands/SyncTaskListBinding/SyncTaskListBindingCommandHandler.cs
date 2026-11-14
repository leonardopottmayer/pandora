using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Google;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.SyncTaskListBinding;

public sealed class SyncTaskListBindingCommandHandler(GoogleTasksSync sync)
    : CommandHandlerBase<SyncTaskListBindingCommand, bool>
{
    protected override Task<Result<bool>> HandleAsync(SyncTaskListBindingCommand request, CancellationToken ct) =>
        sync.SyncAsync(request.Input.BindingId, ct);
}

public sealed class SyncTaskListBindingNowCommandHandler(IUnitOfWorkFactory factory, GoogleTasksSync sync)
    : CommandHandlerBase<SyncTaskListBindingNowCommand, TaskListBindingDto>
{
    protected override async Task<Result<TaskListBindingDto>> HandleAsync(SyncTaskListBindingNowCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var found = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var bindings = context.AcquireRepository<ITaskListBindingRepository>();
            var binding = await bindings.FindAsync(input.UserId, input.BindingId, token);
            if (binding is null)
                return false;

            binding.Resume();
            await bindings.UpdateAsync(binding, token);
            return true;
        }, cancellationToken: ct);
        if (!found)
            return Fail(GoogleTasksErrors.BindingNotFound);

        var synced = await sync.SyncAsync(input.BindingId, ct);
        if (!synced.IsSuccess)
            return Result<TaskListBindingDto>.Failure(synced.Errors);

        var dto = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var binding = await context.AcquireRepository<ITaskListBindingRepository>().FindAsync(input.UserId, input.BindingId, token);
            var list = binding is null ? null
                : await context.AcquireRepository<ITaskListRepository>().FindAsync(input.UserId, binding.ListId, token);
            return binding is null || list is null ? null : binding.ToDto(list);
        }, cancellationToken: ct);

        return dto is null ? Fail(GoogleTasksErrors.BindingNotFound) : Ok(dto);
    }
}
