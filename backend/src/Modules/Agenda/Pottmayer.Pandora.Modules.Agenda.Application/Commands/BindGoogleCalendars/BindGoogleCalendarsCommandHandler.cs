using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Google;
using Pottmayer.Pandora.Modules.Agenda.Application.Mapping;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Commands.BindGoogleCalendars;

public sealed class BindGoogleCalendarsCommandHandler(
    IUnitOfWorkFactory factory, IGoogleCalendarClient google, TimeProvider timeProvider)
    : CommandHandlerBase<BindGoogleCalendarsCommand, IReadOnlyList<CalendarBindingDto>>
{
    protected override async Task<Result<IReadOnlyList<CalendarBindingDto>>> HandleAsync(
        BindGoogleCalendarsCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var wanted = input.RemoteCalendarIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (wanted.Count == 0)
            return Fail(GoogleCalendarErrors.NothingToBind);

        // Reading the account's calendars proves it is a working Google account with the calendar scope.
        var remote = await google.ListCalendarsAsync(input.UserId, input.AccountId, ct);
        if (!remote.IsSuccess)
            return Result<IReadOnlyList<CalendarBindingDto>>.Failure(remote.Errors);

        var byId = remote.Value!.ToDictionary(c => c.Id, StringComparer.Ordinal);
        if (wanted.Any(id => !byId.ContainsKey(id)))
            return Fail(GoogleCalendarErrors.CalendarNotFound);

        return await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, token) =>
        {
            var calendars = context.AcquireRepository<ICalendarRepository>();
            var bindings = context.AcquireRepository<ICalendarBindingRepository>();
            var alreadyBound = (await bindings.GetByAccountAsync(input.AccountId, token))
                .Select(b => b.RemoteCalendarId).ToHashSet(StringComparer.Ordinal);

            var created = new List<CalendarBindingDto>();
            foreach (var id in wanted.Where(id => !alreadyBound.Contains(id)))
            {
                var entry = byId[id];
                var calendar = Calendar.Create(
                    input.UserId, entry.DisplayName, entry.BackgroundColor, isDefault: false,
                    KnownZoneOrUtc(entry.TimeZone), CalendarOrigin.External, timeProvider);
                await calendars.AddAsync(calendar, token);

                // Pandora writes where the account can: its own calendars and those shared with it for editing.
                var direction = entry.AccessRole is "owner" or "writer"
                    ? CalendarBindingDirection.ReadWrite
                    : CalendarBindingDirection.Read;
                var binding = CalendarBinding.Create(input.UserId, calendar.Id, input.AccountId, id, direction, timeProvider);
                await bindings.AddAsync(binding, token);
                created.Add(binding.ToDto(calendar));
            }

            IReadOnlyList<CalendarBindingDto> result = created;
            return Result<IReadOnlyList<CalendarBindingDto>>.Success(result);
        }, cancellationToken: ct);
    }

    private static string KnownZoneOrUtc(string? zone)
    {
        if (string.IsNullOrWhiteSpace(zone))
            return "UTC";
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(zone);
            return zone;
        }
        catch (TimeZoneNotFoundException)
        {
            return "UTC";
        }
    }
}
