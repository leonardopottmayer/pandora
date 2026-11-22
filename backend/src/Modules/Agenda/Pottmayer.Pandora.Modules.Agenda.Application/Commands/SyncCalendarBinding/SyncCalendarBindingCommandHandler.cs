using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Google;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.SyncCalendarBinding;

public sealed class SyncCalendarBindingCommandHandler(GoogleCalendarSync sync)
    : CommandHandlerBase<SyncCalendarBindingCommand, bool>
{
    protected override Task<Result<bool>> HandleAsync(SyncCalendarBindingCommand request, CancellationToken ct) =>
        sync.SyncAsync(request.Input.BindingId, ct);
}

public sealed class SyncCalendarBindingNowCommandHandler(IUnitOfWorkFactory factory, GoogleCalendarSync sync)
    : CommandHandlerBase<SyncCalendarBindingNowCommand, CalendarBindingDto>
{
    protected override async Task<Result<CalendarBindingDto>> HandleAsync(SyncCalendarBindingNowCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var found = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var bindings = context.AcquireRepository<ICalendarBindingRepository>();
            var binding = await bindings.FindAsync(input.UserId, input.BindingId, token);
            if (binding is null)
                return false;

            binding.Resume();
            await bindings.UpdateAsync(binding, token);
            return true;
        }, cancellationToken: ct);
        if (!found)
            return Fail(GoogleCalendarErrors.BindingNotFound);

        var synced = await sync.SyncAsync(input.BindingId, ct);
        if (!synced.IsSuccess)
            return Result<CalendarBindingDto>.Failure(synced.Errors);

        var dto = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var binding = await context.AcquireRepository<ICalendarBindingRepository>().FindAsync(input.UserId, input.BindingId, token);
            var calendar = binding is null ? null
                : await context.AcquireRepository<ICalendarRepository>().FindAsync(input.UserId, binding.CalendarId, token);
            return binding is null || calendar is null ? null : binding.ToDto(calendar);
        }, cancellationToken: ct);

        return dto is null ? Fail(GoogleCalendarErrors.BindingNotFound) : Ok(dto);
    }
}
