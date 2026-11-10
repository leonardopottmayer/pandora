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
        binding.Status.Value,
        binding.LastSyncedAt,
        binding.LastError);
}
