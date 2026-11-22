using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetPreferences;

/// <summary>The defaults when never saved.</summary>
public sealed class GetPreferencesQueryHandler(IUnitOfWorkFactory factory)
    : QueryHandlerBase<GetPreferencesQuery, AgendaPreferencesDto>
{
    protected override async Task<Result<AgendaPreferencesDto>> HandleAsync(
        GetPreferencesQuery request, CancellationToken cancellationToken)
    {
        var preferences = await factory.ExecuteAsync(AgendaModule.DatabaseKey, (ctx, ct) =>
            ctx.AcquireRepository<IAgendaPreferencesRepository>().GetByIdAsync(request.Input.UserId, ct),
            cancellationToken: cancellationToken);

        return Ok(new AgendaPreferencesDto(preferences?.DefaultAlertOffsetMinutes ?? AgendaPreferences.DefaultAlertOffset));
    }
}
