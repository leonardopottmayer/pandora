using System.Text.Json;
using Pottmayer.Pandora.Modules.Notifications.Abstractions;
using Pottmayer.Pandora.Modules.Notifications.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notifications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notifications.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notifications.Application.Enqueue;

/// <summary>
/// Renders a template and persists a <see cref="Notification"/> in the durable queue.
/// Shared by all integration-event subscribers. Idempotent on <paramref name="correlationId"/>.
/// </summary>
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
        CancellationToken ct = default)
    {
        var content = renderer.Render(templateKey, locale, payload);
        var address = Email.Create(recipient);
        var payloadJson = JsonSerializer.Serialize(payload);

        await factory.ExecuteAsync(NotificationsModule.Name, async (context, token) =>
        {
            var notifications = context.AcquireRepository<INotificationRepository>();

            // Dedup: the same integration event must not enqueue twice.
            if (await notifications.ExistsByCorrelationIdAsync(correlationId, token))
                return false;

            var notification = Notification.Queue(
                channel, address, templateKey, locale, payloadJson, content, correlationId, timeProvider);

            await notifications.AddAsync(notification, token);
            return true;
        }, cancellationToken: ct);
    }
}
