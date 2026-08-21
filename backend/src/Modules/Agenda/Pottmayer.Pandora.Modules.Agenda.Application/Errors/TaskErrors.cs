using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Errors;

public static class TaskErrors
{
    public static Error NotFound =>
        Error.NotFound("Agenda.TaskNotFound", "This task does not exist.");

    public static Error TitleRequired =>
        Error.Validation("Agenda.TaskTitleRequired", "A task needs a title.");

    public static Error ListNotFound =>
        Error.NotFound("Agenda.TaskListNotFound", "This task list does not exist.");

    public static Error ParentNotFound =>
        Error.NotFound("Agenda.TaskParentNotFound", "The parent task does not exist.");

    public static Error SubtaskDepthExceeded =>
        Error.Validation("Agenda.TaskSubtaskDepthExceeded", "Subtasks are limited to one level.");

    public static Error ListNotEmpty =>
        Error.Conflict("Agenda.TaskListNotEmpty", "This list still has tasks. Archive it, or move its tasks first.");

    public static Error InvalidRecurrence(string detail) =>
        Error.Validation("Agenda.TaskInvalidRecurrence", detail);
}
