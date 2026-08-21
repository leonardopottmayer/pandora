using Pottmayer.Pandora.Modules.Agenda.Application.Commands.AcknowledgeReminder;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.SnoozeReminder;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Subscribers;

/// <summary>
/// Turns an inline-button tap into a reminder action. It answers only for its own buttons
/// (<c>OwnerModule == "agenda"</c>) and maps each action onto the command the API already exposes, so
/// a tap and a click do the same thing. Acting on an already-handled reminder is a harmless no-op.
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

        // The button carried the reminder id as its payload.
        if (!Guid.TryParse(@event.Payload, out var reminderId))
            return;

        switch (@event.Action)
        {
            case "task_done":
                await sender.Send(
                    new AcknowledgeReminderCommand(new AcknowledgeReminderInput(@event.UserId, reminderId)),
                    cancellationToken);
                break;

            case "snooze_1h":
                await sender.Send(
                    new SnoozeReminderCommand(new SnoozeReminderInput(
                        @event.UserId, reminderId, timeProvider.GetUtcNow() + SnoozeInterval)),
                    cancellationToken);
                break;
        }
    }
}
