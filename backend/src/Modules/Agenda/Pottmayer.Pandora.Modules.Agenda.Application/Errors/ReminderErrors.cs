using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Errors;

public static class ReminderErrors
{
    public static Error NotFound =>
        Error.NotFound("Agenda.ReminderNotFound", "This reminder does not exist.");

    public static Error TitleRequired =>
        Error.Validation("Agenda.ReminderTitleRequired", "A reminder needs a title.");

    public static Error RecurringCannotBeRescheduled =>
        Error.Validation("Agenda.ReminderRecurringCannotBeRescheduled", "A recurring reminder is moved per occurrence, not rescheduled.");

    public static Error InvalidRecurrence(string detail) =>
        Error.Validation("Agenda.ReminderInvalidRecurrence", detail);
}
