using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Google;

/// <summary>
/// Google → Pandora only: an event of a calendar fed by Google is written by the sync alone. The event,
/// alert and calendar handlers ask here before writing.
/// </summary>
internal static class ExternalCalendars
{
    public static async Task<bool> IsReadOnlyAsync(IDataContext context, Guid userId, Guid calendarId, CancellationToken ct) =>
        await context.AcquireRepository<ICalendarRepository>().FindAsync(userId, calendarId, ct) is { IsExternal: true };
}
