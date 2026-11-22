using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Errors;

public static class EventErrors
{
    public static Error NotFound =>
        Error.NotFound("Agenda.EventNotFound", "This event does not exist.");

    public static Error TitleRequired =>
        Error.Validation("Agenda.EventTitleRequired", "An event needs a title.");

    public static Error CalendarNotFound =>
        Error.NotFound("Agenda.EventCalendarNotFound", "This calendar does not exist.");

    public static Error InvalidScope =>
        Error.Validation("Agenda.EventInvalidScope", "Scope must be one of: this, this-and-future, all.");

    public static Error NotRecurring =>
        Error.Validation("Agenda.EventNotRecurring", "A single event has no occurrence to scope; edit it with scope=all.");

    public static Error OccurrenceRequired =>
        Error.Validation("Agenda.EventOccurrenceRequired", "This scope needs the occurrence's original start.");

    public static Error Invalid(string detail) =>
        Error.Validation("Agenda.EventInvalid", detail);

    public static Error RangeInvalid =>
        Error.Validation("Agenda.EventRangeInvalid", "`to` must be on or after `from`.");

    public static Error RangeTooLarge =>
        Error.Validation("Agenda.EventRangeTooLarge", "The requested range is too large; query at most 366 days.");
}
