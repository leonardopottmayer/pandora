using Pottmayer.Pandora.Modules.Channels.Application.Enqueue;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Application.Subscribers;

/// <summary>
/// Handles the generic <see cref="SendNotificationRequested"/> escape hatch: the caller already
/// chose channel, template and recipient.
/// </summary>
public sealed class SendNotificationRequestedHandler(NotificationEnqueuer enqueuer)
    : IIntegrationEventHandler<SendNotificationRequested>
{
    public Task HandleAsync(SendNotificationRequested @event, CancellationToken cancellationToken = default)
    {
        return enqueuer.EnqueueAsync(
            Channel.FromValue(@event.Channel),
            @event.Recipient,
            TemplateKey.Create(@event.TemplateKey),
            Locale.Normalize(@event.Locale),
            @event.Payload,
            @event.EventId,
            ct: cancellationToken);
    }
}
