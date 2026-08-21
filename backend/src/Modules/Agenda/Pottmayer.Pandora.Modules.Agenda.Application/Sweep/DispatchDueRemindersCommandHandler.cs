using Pottmayer.Pandora.Modules.Agenda.Abstractions;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Sweep;

/// <summary>
/// The scan that fires due reminders. For each, it publishes one <see cref="NotifyUserRequested"/> —
/// naming the category, template and inline buttons, which is what only Agenda knows — and flips the
/// reminder to <c>Notified</c> in the same unit of work. The status flip is the idempotency guard:
/// a committed reminder is no longer selected, so nothing re-fires across restarts.
/// </summary>
public sealed class DispatchDueRemindersCommandHandler(
    IUnitOfWorkFactory factory,
    IIntegrationEventBus bus,
    TimeProvider timeProvider)
    : CommandHandlerBase<DispatchDueRemindersCommand, int>
{
    private const string TemplateKey = "agenda.reminder.due";

    protected override async Task<Result<int>> HandleAsync(DispatchDueRemindersCommand request, CancellationToken ct)
    {
        var events = await factory.ExecuteAsync(AgendaModule.Name, async (context, token) =>
        {
            var reminders = context.AcquireRepository<IReminderRepository>();
            var now = timeProvider.GetUtcNow();
            var due = await reminders.GetDueAsync(now, request.Input.BatchSize, token);

            var toPublish = new List<NotifyUserRequested>(due.Count);
            foreach (var reminder in due)
            {
                toPublish.Add(BuildNotification(reminder, now));
                reminder.MarkNotified();
                await reminders.UpdateAsync(reminder, token);
            }

            return toPublish;
        }, cancellationToken: ct);

        // After commit: the fire is only true once the reminder is durably notified.
        foreach (var evt in events)
            await bus.PublishAsync(evt, ct);

        return Ok(events.Count);
    }

    private NotifyUserRequested BuildNotification(Reminder reminder, DateTimeOffset now)
    {
        var reminderId = reminder.Id.ToString();
        return new NotifyUserRequested(
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
            // A fresh correlation id per firing: a snooze re-fire is a new notification, not a dup.
            CorrelationId: Guid.CreateVersion7(),
            Buttons:
            [
                new NotificationButton("agenda", "task_done", "✓ Concluir", reminderId),
                new NotificationButton("agenda", "snooze_1h", "⏰ Adiar 1h", reminderId),
            ]);
    }
}
