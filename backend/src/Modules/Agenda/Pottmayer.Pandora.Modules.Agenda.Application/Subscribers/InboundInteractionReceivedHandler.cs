using Pottmayer.Pandora.Modules.Agenda.Application.Commands.AcknowledgeOccurrence;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.AcknowledgeReminder;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.SnoozeOccurrence;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.SnoozeReminder;
using Pottmayer.Pandora.Modules.Agenda.Application.Reminders;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Subscribers;

/// <summary>
/// Turns an inline-button tap into a reminder action. It answers only for its own buttons
/// (<c>OwnerModule == "agenda"</c>) and maps each action onto a command. The button payload tells the
/// two kinds apart: a bare reminder id is single-shot (acts on the reminder); a
/// <c>reminderId|occurrence</c> pair is a recurring occurrence (acts on that occurrence's dispatch
/// row, leaving the series running). Acting on an already-handled item is a harmless no-op.
/// </summary>
public sealed class InboundInteractionReceivedHandler(ISender sender, TimeProvider timeProvider)
    : IIntegrationEventHandler<InboundInteractionReceived>
{
    private const string OwnerModule = "agenda";
    private static readonly TimeSpan SnoozeInterval = TimeSpan.FromHours(1);

    public async Task HandleAsync(InboundInteractionReceived @event, CancellationToken cancellationToken = default)
    {
        if (@event.OwnerModule != OwnerModule)
            return;

        if (!ReminderButtonPayload.TryParse(@event.Payload, out var reminderId, out var occurrenceStartsAt))
            return;

        var snoozeUntil = timeProvider.GetUtcNow() + SnoozeInterval;

        switch (@event.Action, occurrenceStartsAt)
        {
            // Recurring: act on the occurrence, series continues.
            case ("task_done", { } occurrence):
                await sender.Send(
                    new AcknowledgeOccurrenceCommand(new AcknowledgeOccurrenceInput(@event.UserId, reminderId, occurrence)),
                    cancellationToken);
                break;

            case ("snooze_1h", { } occurrence):
                await sender.Send(
                    new SnoozeOccurrenceCommand(new SnoozeOccurrenceInput(@event.UserId, reminderId, occurrence, snoozeUntil)),
                    cancellationToken);
                break;

            // Single-shot: act on the reminder itself (Phase 1 behaviour).
            case ("task_done", null):
                await sender.Send(
                    new AcknowledgeReminderCommand(new AcknowledgeReminderInput(@event.UserId, reminderId)),
                    cancellationToken);
                break;

            case ("snooze_1h", null):
                await sender.Send(
                    new SnoozeReminderCommand(new SnoozeReminderInput(@event.UserId, reminderId, snoozeUntil)),
                    cancellationToken);
                break;
        }
    }
}
