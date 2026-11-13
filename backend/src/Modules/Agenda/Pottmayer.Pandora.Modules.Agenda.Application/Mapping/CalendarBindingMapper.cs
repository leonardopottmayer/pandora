using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Mapping;

internal static class CalendarBindingMapper
{
    public static CalendarBindingDto ToDto(this CalendarBinding binding, Calendar calendar) => new(
        binding.Id,
        binding.CalendarId,
        calendar.Name,
        calendar.Color,
        binding.ExternalAccountId,
        binding.RemoteCalendarId,
        binding.Direction.Value,
        binding.Status.Value,
        binding.LastSyncedAt,
        binding.LastError);

    public static SyncConflictDto ToDto(this SyncConflict conflict) => new(
        conflict.Id, conflict.EventId, conflict.Title, conflict.LosingSide.Value, conflict.LosingVersion, conflict.DetectedAt);
}
