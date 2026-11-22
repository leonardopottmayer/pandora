using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Mapping;

internal static class ReminderMapper
{
    public static ReminderDto ToDto(this Reminder reminder) => new(
        reminder.Id,
        reminder.Title,
        reminder.Notes,
        reminder.RemindAt,
        reminder.TimeZone,
        reminder.Rrule,
        reminder.RecurrenceEndsAt,
        reminder.Status.ToString(),
        reminder.SnoozedUntil,
        reminder.AcknowledgedAt);
}
