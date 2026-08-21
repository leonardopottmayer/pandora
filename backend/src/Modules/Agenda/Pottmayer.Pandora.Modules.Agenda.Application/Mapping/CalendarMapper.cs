using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Mapping;

internal static class CalendarMapper
{
    public static CalendarDto ToDto(this Calendar calendar) => new(
        calendar.Id,
        calendar.Name,
        calendar.Color,
        calendar.IsDefault,
        calendar.IsVisible,
        calendar.TimeZone,
        calendar.Origin.ToString(),
        calendar.ArchivedAt);

    public static EventDto ToDto(this Event ev) => new(
        ev.Id,
        ev.CalendarId,
        ev.Title,
        ev.Description,
        ev.Location,
        ev.Url,
        ev.StartsAt,
        ev.EndsAt,
        ev.IsAllDay,
        ev.TimeZone,
        ev.Rrule,
        ev.RecurrenceEndsAt,
        ev.Status.ToString());

    public static EventOccurrenceDto ToDto(this EventOccurrence occ) => new(
        occ.EventId,
        occ.CalendarId,
        occ.OriginalStartsAt,
        occ.StartsAt,
        occ.EndsAt,
        occ.IsAllDay,
        occ.Title,
        occ.Description,
        occ.Location,
        occ.Url,
        occ.Status.ToString());
}
