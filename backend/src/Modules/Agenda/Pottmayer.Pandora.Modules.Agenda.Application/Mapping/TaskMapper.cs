using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Mapping;

internal static class TaskMapper
{
    public static TaskListDto ToDto(this TaskList list) =>
        new(list.Id, list.Name, list.IsDefault, list.Position, list.ArchivedAt);

    public static TaskDto ToDto(this TaskItem task) => new(
        task.Id,
        task.ListId,
        task.ParentTaskId,
        task.Title,
        task.Notes,
        task.DueAt,
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
