using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Google;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetGoogleCalendars;

public sealed class GetGoogleCalendarsQueryHandler(IUnitOfWorkFactory factory, IGoogleCalendarClient google)
    : QueryHandlerBase<GetGoogleCalendarsQuery, IReadOnlyList<GoogleCalendarDto>>
{
    protected override async Task<Result<IReadOnlyList<GoogleCalendarDto>>> HandleAsync(
        GetGoogleCalendarsQuery request, CancellationToken cancellationToken)
    {
        var input = request.Input;
        var remote = await google.ListCalendarsAsync(input.UserId, input.AccountId, cancellationToken);
        if (!remote.IsSuccess)
            return Fail(remote.Errors);

        var bound = await factory.ExecuteAsync(AgendaModule.DatabaseKey, (context, ct) =>
            context.AcquireRepository<ICalendarBindingRepository>().GetByAccountAsync(input.AccountId, ct),
            cancellationToken: cancellationToken);
        var bindingOf = bound.Where(b => b.UserId == input.UserId).ToDictionary(b => b.RemoteCalendarId, b => b.Id);

        IReadOnlyList<GoogleCalendarDto> dtos =
        [
            .. remote.Value!
                .OrderByDescending(c => c.Primary == true)
                .ThenBy(c => c.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(c => new GoogleCalendarDto(
                    c.Id, c.DisplayName, c.BackgroundColor, c.Primary == true, c.AccessRole,
                    bindingOf.TryGetValue(c.Id, out var bindingId) ? bindingId : null))
        ];
        return Ok(dtos);
    }
}
