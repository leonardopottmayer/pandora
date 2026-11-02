using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.SavePreferences;

public sealed class SavePreferencesCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<SavePreferencesCommand, AgendaPreferencesDto>
{
    protected override async Task<Result<AgendaPreferencesDto>> HandleAsync(SavePreferencesCommand request, CancellationToken ct)
    {
        var input = request.Input;

        await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IAgendaPreferencesRepository>();
            var preferences = await repo.GetByIdAsync(input.UserId, token);
            if (preferences is null)
            {
                await repo.AddAsync(AgendaPreferences.Create(input.UserId, input.DefaultAlertOffsetMinutes), token);
                return;
            }

            preferences.Update(input.DefaultAlertOffsetMinutes);
            await repo.UpdateAsync(preferences, token);
        }, cancellationToken: ct);

        return Ok(new AgendaPreferencesDto(input.DefaultAlertOffsetMinutes));
    }
}
