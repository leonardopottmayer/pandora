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

    /// <summary>The calendar is fed by Google: its events change in Google, and come here on the next sync.</summary>
    public static Error ReadOnly =>
        Error.Conflict("Agenda.CalendarReadOnly", "This calendar comes from Google Calendar. Edit its events in Google; they sync here.");

    public static Error MoveAcrossGoogle =>
        Error.Conflict("Agenda.EventMoveAcrossGoogle", "An event in a Google calendar stays in it, and an event cannot move into one. Create it again in the other calendar.");

    public static Error Bound =>
        Error.Conflict("Agenda.CalendarBound", "This calendar is linked to Google Calendar. Unlink it in the Agenda settings first.");

    public static Error InvalidTimeZone(string detail) =>
        Error.Validation("Agenda.CalendarInvalidTimeZone", detail);
}
