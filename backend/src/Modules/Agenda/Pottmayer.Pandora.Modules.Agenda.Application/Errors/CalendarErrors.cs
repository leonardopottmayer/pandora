using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Errors;

public static class CalendarErrors
{
    public static Error NotFound =>
        Error.NotFound("Agenda.CalendarNotFound", "This calendar does not exist.");

    public static Error NameRequired =>
        Error.Validation("Agenda.CalendarNameRequired", "A calendar needs a name.");

    public static Error NotEmpty =>
        Error.Conflict("Agenda.CalendarNotEmpty", "This calendar still has events. Archive it, or move its events first.");

    public static Error InvalidTimeZone(string detail) =>
        Error.Validation("Agenda.CalendarInvalidTimeZone", detail);
}
