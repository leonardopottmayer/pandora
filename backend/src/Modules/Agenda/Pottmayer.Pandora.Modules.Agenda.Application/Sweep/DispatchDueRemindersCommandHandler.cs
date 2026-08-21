using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Application.Reminders;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Agenda.Domain.Recurrence;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Sweep;

/// <summary>
/// The scan that fires due reminders. For each firing it publishes one <see cref="NotifyUserRequested"/>
/// — naming the category, template and inline buttons, which is what only Agenda knows — inside the
/// same unit of work that records the fire, then publishes after commit (a fire is only true once
/// durable).
///
/// <para>Two idempotency guards, one per kind. A <b>single-shot</b> reminder flips to
/// <c>Notified</c>, so a committed one is no longer selected. A <b>recurring</b> reminder expands its
/// occurrences within the grace window and writes a per-occurrence ledger row (unique on
/// <c>(reminder, occurrence)</c>); an occurrence with a row is skipped, so re-running a tick or
/// restarting mid-tick never double-fires. Snoozed occurrences re-fire once when their snooze passes.</para>
/// </summary>
public sealed class DispatchDueRemindersCommandHandler(
    IUnitOfWorkFactory factory,
    IIntegrationEventBus bus,
    IOptions<AgendaOptions> options,
    TimeProvider timeProvider)
    : CommandHandlerBase<DispatchDueRemindersCommand, int>
{
    private const string TemplateKey = "agenda.reminder.due";

    // Older than this after its occurrence anchor, a firing is flagged late (delivered from the grace
    // window rather than on the tick it came due).
    private static readonly TimeSpan LateThreshold = TimeSpan.FromMinutes(1);

    private readonly AgendaOptions _options = options.Value;

    protected override async Task<Result<int>> HandleAsync(DispatchDueRemindersCommand request, CancellationToken ct)
    {
        var batchSize = request.Input.BatchSize;
        var grace = TimeSpan.FromMinutes(Math.Max(0, _options.SweepGraceMinutes));

        var events = await factory.ExecuteAsync(AgendaModule.Name, async (context, token) =>
        {
            var reminders = context.AcquireRepository<IReminderRepository>();
            var dispatches = context.AcquireRepository<IReminderDispatchRepository>();
            var now = timeProvider.GetUtcNow();
            var windowStart = now - grace;

            var toPublish = new List<NotifyUserRequested>();

            await FireSingleShotAsync(reminders, now, batchSize, toPublish, token);
            await FireRecurringAsync(reminders, dispatches, now, windowStart, batchSize, toPublish, token);
            await ReFireSnoozedAsync(reminders, dispatches, now, batchSize, toPublish, token);

            return toPublish;
        }, cancellationToken: ct);

        // After commit: the fire is only true once it is durably recorded.
        foreach (var evt in events)
            await bus.PublishAsync(evt, ct);

        return Ok(events.Count);
    }

    // ── Single-shot: the Phase 1 status-guarded path, untouched ──
    private async Task FireSingleShotAsync(
        IReminderRepository reminders, DateTimeOffset now, int batchSize,
        List<NotifyUserRequested> toPublish, CancellationToken token)
    {
        var due = await reminders.GetDueAsync(now, batchSize, token);
        foreach (var reminder in due)
        {
            toPublish.Add(BuildNotification(reminder, ReminderButtonPayload.ForReminder(reminder.Id), now, Guid.CreateVersion7()));
            reminder.MarkNotified();
            await reminders.UpdateAsync(reminder, token);
        }
    }

    // ── Recurring: expand occurrences in the window, fire the ones without a ledger row ──
    private async Task FireRecurringAsync(
        IReminderRepository reminders, IReminderDispatchRepository dispatches,
        DateTimeOffset now, DateTimeOffset windowStart, int batchSize,
        List<NotifyUserRequested> toPublish, CancellationToken token)
    {
        var recurring = await reminders.GetActiveRecurringAsync(windowStart, batchSize, token);
        foreach (var reminder in recurring)
        {
            var zone = reminder.ResolveZone();
            var rule = RecurrenceRule.Parse(reminder.Rrule!);
            var occurrences = rule.Expand(reminder.RemindAt, windowStart, now, zone).ToList();
            if (occurrences.Count == 0)
                continue;

            var alreadyDispatched = (await dispatches.GetDispatchedOccurrencesAsync(reminder.Id, occurrences, token))
                .ToHashSet();

            foreach (var occurrence in occurrences)
            {
                if (alreadyDispatched.Contains(occurrence))
                    continue;

                var correlationId = Guid.CreateVersion7();
                toPublish.Add(BuildNotification(
                    reminder, ReminderButtonPayload.ForOccurrence(reminder.Id, occurrence), now, correlationId));

                var isLate = now - occurrence > LateThreshold;
                await dispatches.AddAsync(
                    ReminderDispatch.Record(reminder.Id, reminder.UserId, occurrence, correlationId, isLate, timeProvider),
                    token);
            }
        }
    }

    // ── Snooze re-fire: an occurrence deferred by the user, now due again ──
    private async Task ReFireSnoozedAsync(
        IReminderRepository reminders, IReminderDispatchRepository dispatches, DateTimeOffset now, int batchSize,
        List<NotifyUserRequested> toPublish, CancellationToken token)
    {
        var snoozed = await dispatches.GetSnoozeDueAsync(now, batchSize, token);
        foreach (var dispatch in snoozed)
        {
            var reminder = await reminders.FindAsync(dispatch.UserId, dispatch.ReminderId, token);
            if (reminder is null || reminder.Status == Domain.ValueObjects.ReminderStatus.Cancelled)
            {
                dispatch.ClearSnooze();
                await dispatches.UpdateAsync(dispatch, token);
                continue;
            }

            // A fresh correlation id: a re-fire is a new delivery, and Channels de-dups on the id, so
            // reusing the original would drop it.
            toPublish.Add(BuildNotification(
                reminder,
                ReminderButtonPayload.ForOccurrence(dispatch.ReminderId, dispatch.OccurrenceStartsAt),
                now,
                Guid.CreateVersion7()));

            dispatch.ClearSnooze(); // one snooze, one re-fire
            await dispatches.UpdateAsync(dispatch, token);
        }
    }

    private static NotifyUserRequested BuildNotification(
        Reminder reminder, string buttonPayload, DateTimeOffset now, Guid correlationId) =>
        new(
            EventId: Guid.CreateVersion7(),
            OccurredAt: now,
            UserId: reminder.UserId,
            Category: AgendaModule.ReminderCategory,
            TemplateKey: TemplateKey,
            Locale: null, // Channels renders in the user's channel locale.
            Channels: null, // Channels resolves from the user's preference.
            Payload: new Dictionary<string, string>
            {
                ["title"] = reminder.Title,
                ["notes"] = reminder.Notes ?? string.Empty,
            },
            CorrelationId: correlationId,
            Buttons:
            [
                new NotificationButton("agenda", "task_done", "✓ Concluir", buttonPayload),
                new NotificationButton("agenda", "snooze_1h", "⏰ Adiar 1h", buttonPayload),
            ]);
}
