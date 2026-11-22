using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Recurrence;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Mapping;

internal static class TaskMapper
{
    public static TaskListDto ToDto(this TaskList list) =>
        new(list.Id, list.Name, list.IsDefault, list.Position, list.ArchivedAt);

    /// <summary>
    /// With a <paramref name="viewerZone"/>, a date-only due comes back as the start of the same date in
    /// that zone, so it reads as its own day wherever the task was created.
    /// </summary>
    public static TaskDto ToDto(this TaskItem task, TimeZoneInfo? viewerZone = null) => new(
        task.Id,
        task.ListId,
        task.ParentTaskId,
        task.Title,
        task.Notes,
        viewerZone is not null && !task.DueHasTime && task.LocalDueDate() is { } date
            ? AllDayDates.StartOf(date, viewerZone)
            : task.DueAt,
        task.DueHasTime,
        task.Priority.ToString(),
        task.Status.ToString(),
        task.CompletedAt,
        task.TimeZone,
        task.Rrule,
        task.Position);

    public static AlertDto ToDto(this Alert alert) =>
        new(alert.Id, alert.SubjectType.ToString(), alert.SubjectId, alert.OffsetMinutes, alert.Channels, alert.IsEnabled);
}
