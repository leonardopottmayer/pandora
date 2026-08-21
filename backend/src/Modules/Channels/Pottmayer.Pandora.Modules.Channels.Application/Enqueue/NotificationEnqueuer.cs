using System.Text.Json;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Channels.Application.Enqueue;

/// <summary>
/// Renders a template and persists a <see cref="Notification"/> in the durable queue.
/// Shared by all integration-event subscribers. Idempotent on <paramref name="correlationId"/>.
/// </summary>
/// <remarks>
/// Rendering happens here, not at send time: what went out is on the row, a retry re-sends the same
/// bytes, and editing a template tomorrow does not rewrite history.
/// </remarks>
public sealed class NotificationEnqueuer(
    IUnitOfWorkFactory factory,
    INotificationTemplateRenderer renderer,
    TimeProvider timeProvider)
{
    public async Task EnqueueAsync(
        Channel channel,
        string recipient,
        TemplateKey templateKey,
        string locale,
        IReadOnlyDictionary<string, string> payload,
        Guid correlationId,
        string? renderedPayload = null,
        Guid? groupId = null,
        CancellationToken ct = default)
    {
        var content = renderer.Render(templateKey, channel, locale, payload);
        var address = NotificationAddress.Create(channel, recipient);
        var payloadJson = JsonSerializer.Serialize(payload);

        await factory.ExecuteAsync(ChannelsModule.Name, async (context, token) =>
        {
            var notifications = context.AcquireRepository<INotificationRepository>();

            // Dedup per channel: the same integration event must not enqueue twice on one channel,
            // but a fan-out legitimately writes the same correlation id once per channel.
            if (await notifications.ExistsByCorrelationAndChannelAsync(correlationId, channel, token))
                return false;

            var notification = Notification.Queue(
                channel, address, templateKey, locale, payloadJson, content, correlationId, timeProvider,
                renderedPayload, groupId);

            await notifications.AddAsync(notification, token);
            return true;
        }, cancellationToken: ct);
    }
}
