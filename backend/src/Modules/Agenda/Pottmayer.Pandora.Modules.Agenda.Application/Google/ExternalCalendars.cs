using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Google;

/// <summary>
/// What the event, alert and calendar handlers ask before and after writing in a calendar fed by Google.
/// A <c>read</c> binding keeps the calendar read-only; a <c>read-write</c> one lets the write through and
/// marks the event for the next push, in the same transaction.
/// </summary>
internal static class ExternalCalendars
{
    public static async Task<bool> IsReadOnlyAsync(IDataContext context, Guid userId, Guid calendarId, CancellationToken ct)
    {
        if (await context.AcquireRepository<ICalendarRepository>().FindAsync(userId, calendarId, ct) is not { IsExternal: true })
            return false;
        var binding = await context.AcquireRepository<ICalendarBindingRepository>().FindByCalendarAsync(calendarId, ct);
        return binding is not { CanWrite: true };
    }

    /// <summary>Whether the calendar is bound to Google at all, either way.</summary>
    public static async Task<bool> IsBoundAsync(IDataContext context, Guid calendarId, CancellationToken ct) =>
        await context.AcquireRepository<ICalendarBindingRepository>().IsCalendarBoundAsync(calendarId, ct);

    /// <summary>
    /// Pandora changed <paramref name="ev"/> (created, edited, deleted, an occurrence or an alert): when its
    /// calendar sends edits to Google, the event's link waits for the next push. A new event gets its link here.
    /// </summary>
    public static async Task MarkChangedAsync(IDataContext context, Event ev, TimeProvider timeProvider, CancellationToken ct)
    {
        var binding = await context.AcquireRepository<ICalendarBindingRepository>().FindByCalendarAsync(ev.CalendarId, ct);
        if (binding is not { CanWrite: true })
            return;

        var links = context.AcquireRepository<IEventLinkRepository>();
        var link = await links.FindByEventAsync(ev.Id, ct);
        if (link is null)
        {
            if (ev.DeletedAt is null)
                await links.AddAsync(EventLink.CreateLocal(binding.Id, ev.Id, timeProvider), ct);
            return;
        }

        link.MarkPending(timeProvider.GetUtcNow());
        await links.UpdateAsync(link, ct);
    }
}
