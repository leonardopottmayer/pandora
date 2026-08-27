using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Application.Commands.DispatchPending;

/// <summary>
/// Drains the durable queue: loads due notifications, hands each to the transport for its channel,
/// and records the outcome (sent / failed-with-backoff / dead). One transaction per batch.
/// </summary>
public sealed class DispatchPendingNotificationsCommandHandler(
    IUnitOfWorkFactory factory,
    IEnumerable<IChannelTransport> transports,
    IIntegrationEventBus bus,
    TimeProvider timeProvider)
    : CommandHandlerBase<DispatchPendingNotificationsCommand, DispatchPendingNotificationsResult>
{
    protected override async Task<Result<DispatchPendingNotificationsResult>> HandleAsync(
        DispatchPendingNotificationsCommand request, CancellationToken ct)
    {
        var byChannel = transports.ToDictionary(t => t.Channel);
        var disabled = new List<UserChannelDisabled>();

        var result = await factory.ExecuteAsync(ChannelsModule.DatabaseKey, async (context, token) =>
        {
            var notifications = context.AcquireRepository<INotificationRepository>();
            var userChannels = context.AcquireRepository<IUserChannelRepository>();
            var now = timeProvider.GetUtcNow();
            var due = await notifications.GetDueAsync(now, request.Input.BatchSize, token);

            var sent = 0;
            var failed = 0;
            var dead = 0;

            foreach (var notification in due)
            {
                notification.MarkSending();

                try
                {
                    if (!byChannel.TryGetValue(notification.Channel, out var transport))
                    {
                        throw new PermanentDeliveryException(
                            $"No transport is registered for channel '{notification.Channel.Value}'.");
                    }

                    var delivery = await transport.SendAsync(notification, token);
                    notification.MarkSent(delivery.Provider, delivery.ProviderMessageId);
                    sent++;
                }
                catch (PermanentDeliveryException ex)
                {
                    // Retrying cannot help. Kill the row now and stop using the address, instead of
                    // burning five attempts against a wall.
                    notification.MarkDead(ex.Message);
                    dead++;

                    if (await DisableAddressAsync(userChannels, notification, ex.Message, token) is { } evt)
                        disabled.Add(evt);
                }
                catch (Exception ex)
                {
                    notification.MarkFailed(ex.Message, timeProvider);
                    if (notification.Status == NotificationStatus.Dead)
                        dead++;
                    else
                        failed++;
                }

                await notifications.UpdateAsync(notification, token);
            }

            return new DispatchPendingNotificationsResult(sent, failed, dead);
        }, cancellationToken: ct);

        // After the unit of work committed: the fact is only true once the disable is durable.
        foreach (var evt in disabled)
            await bus.PublishAsync(evt, ct);

        return Ok(result);
    }

    private async Task<UserChannelDisabled?> DisableAddressAsync(
        IUserChannelRepository userChannels,
        Notification notification,
        string reason,
        CancellationToken ct)
    {
        var link = await userChannels.FindByAddressAsync(notification.Channel, notification.Address, ct);
        if (link is null || !link.IsEnabled)
            return null;

        link.Disable(reason);
        await userChannels.UpdateAsync(link, ct);

        return new UserChannelDisabled(
            Guid.CreateVersion7(),
            timeProvider.GetUtcNow(),
            link.UserId,
            link.Channel.Value,
            reason);
    }
}
