using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Errors;

public static class AlertErrors
{
    public static Error NotFound =>
        Error.NotFound("Agenda.AlertNotFound", "This alert does not exist.");

    public static Error SubjectNotFound =>
        Error.NotFound("Agenda.AlertSubjectNotFound", "The subject of this alert does not exist.");

    public static Error UnsupportedSubjectType =>
        Error.Validation("Agenda.AlertUnsupportedSubjectType", "Alerts are only supported on tasks and events in this version.");

    public static Error SubjectHasNoDueDate =>
        Error.Validation("Agenda.AlertSubjectHasNoDueDate", "A task needs a due date before it can carry an alert.");
}
