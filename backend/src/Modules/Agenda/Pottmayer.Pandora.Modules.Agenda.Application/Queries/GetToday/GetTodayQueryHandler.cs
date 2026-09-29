using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Preferences;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.Recurrence;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetToday;

/// <summary>
/// One read that merges the three time sources for the day: expanded event occurrences, tasks due, and
/// reminders firing (single-shot and recurring occurrences). Ordered by start time. The day window is
/// anchored in the user's own zone, so "today" is their calendar day rather than the UTC day. A span of
/// days (<see cref="GetTodayInput.From"/>..<see cref="GetTodayInput.To"/>) reads the same way.
/// </summary>
public sealed class GetTodayQueryHandler(
    IUnitOfWorkFactory factory, IEffectiveTimeZoneResolver timeZones, TimeProvider timeProvider)
    : QueryHandlerBase<GetTodayQuery, IReadOnlyList<TodayItemDto>>
{
    protected override async Task<Result<IReadOnlyList<TodayItemDto>>> HandleAsync(
        GetTodayQuery request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var zone = await timeZones.ResolveAsync(request.Input.UserId, ct: cancellationToken);
        var firstDay = request.Input.From ?? DayBoundary.LocalToday(zone, now);
        var lastDay = request.Input.To ?? firstDay;
        var dayStart = DayBoundary.StartOfDay(zone, firstDay);
        var dayEnd = DayBoundary.StartOfDay(zone, lastDay.AddDays(1));
        var expandTo = dayEnd.AddTicks(-1); // inclusive upper bound for the expanders

        var items = await factory.ExecuteAsync(AgendaModule.DatabaseKey, async (context, ct) =>
        {
            var result = new List<TodayItemDto>();

            // ── Events ──
            var events = context.AcquireRepository<IEventRepository>();
            var overrides = context.AcquireRepository<IEventOccurrenceOverrideRepository>();
            var eventRows = await events.GetOverlappingAsync(request.Input.UserId, null, dayStart, expandTo, ct);
            if (eventRows.Count > 0)
            {
                var overrideRows = await overrides.GetByEventsAsync([.. eventRows.Select(e => e.Id)], ct);
                var byEvent = overrideRows
                    .GroupBy(o => o.EventId)
                    .ToDictionary(g => g.Key, g => (IReadOnlyList<EventOccurrenceOverride>)[.. g]);

                foreach (var ev in eventRows)
                {
                    var forEvent = byEvent.TryGetValue(ev.Id, out var list) ? list : [];
                    foreach (var occ in EventExpander.Expand(ev, forEvent, dayStart, expandTo))
                        result.Add(new TodayItemDto(
                            "event", occ.EventId, occ.Title, occ.Description, occ.StartsAt, occ.EndsAt,
                            occ.IsAllDay, occ.CalendarId, occ.Status.ToString()));
                }
            }

            // ── Tasks due today ──
            var tasks = context.AcquireRepository<ITaskRepository>();
            foreach (var task in await tasks.GetByUserAsync(request.Input.UserId, null, null, ct))
            {
                if (task.DueAt is { } due && due >= dayStart && due < dayEnd)
                    result.Add(new TodayItemDto(
                        "task", task.Id, task.Title, task.Notes, due, null, !task.DueHasTime, null,
                        task.Status.ToString()));
            }

            // ── Reminders firing today ──
            var reminders = context.AcquireRepository<IReminderRepository>();
            foreach (var reminder in await reminders.GetByUserAsync(request.Input.UserId, ct))
            {
                if (reminder.Status == ReminderStatus.Cancelled)
                    continue;

                if (reminder.IsRecurring)
                {
                    var rule = RecurrenceRule.Parse(reminder.Rrule!);
                    foreach (var occ in rule.Expand(reminder.RemindAt, dayStart, expandTo, reminder.ResolveZone()))
                        result.Add(new TodayItemDto(
                            "reminder", reminder.Id, reminder.Title, reminder.Notes, occ, null, false, null,
                            reminder.Status.ToString()));
                }
                else if (reminder.RemindAt >= dayStart && reminder.RemindAt < dayEnd)
                {
                    result.Add(new TodayItemDto(
                        "reminder", reminder.Id, reminder.Title, reminder.Notes, reminder.RemindAt, null, false, null,
                        reminder.Status.ToString()));
                }
            }

            return result;
        }, cancellationToken: cancellationToken);

        IReadOnlyList<TodayItemDto> ordered = [.. items.OrderBy(i => i.At)];
        return Ok(ordered);
    }
}
