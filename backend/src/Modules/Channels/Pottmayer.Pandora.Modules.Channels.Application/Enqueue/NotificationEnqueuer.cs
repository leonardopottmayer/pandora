using System.Text.Json;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Channels.Domain.Rendering;
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
    // How long a rendered inline button stays actionable. A button from yesterday is expired, not
    // a fresh command.
    private static readonly TimeSpan InteractionLifetime = TimeSpan.FromHours(24);

    public async Task EnqueueAsync(
        Channel channel,
        string recipient,
        TemplateKey templateKey,
        string locale,
        IReadOnlyDictionary<string, string> payload,
        Guid correlationId,
        string? renderedPayload = null,
        Guid? groupId = null,
        Guid? userId = null,
        string? category = null,
        IReadOnlyList<NotificationButton>? buttons = null,
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
                renderedPayload, groupId, userId, category);

            await notifications.AddAsync(notification, token);

            // Buttons only materialize on a channel that renders them. Registering each as an
            // interaction — in this same unit of work, so the FK to the notification holds — is what
            // lets a 64-byte callback resolve back to a full action later.
            if (channel == Channel.Telegram && userId is { } owner && buttons is { Count: > 0 })
                await AttachButtonsAsync(context, notification, owner, buttons, content.Body, token);

            return true;
        }, cancellationToken: ct);
    }

    private async Task AttachButtonsAsync(
        Pottmayer.Tars.Data.Abstractions.DataContext.IDataContext context,
        Notification notification,
        Guid userId,
        IReadOnlyList<NotificationButton> buttons,
        string text,
        CancellationToken ct)
    {
        var interactions = context.AcquireRepository<IInteractionRepository>();
        var expiresAt = timeProvider.GetUtcNow() + InteractionLifetime;

        var rendered = new List<TelegramRenderedButton>(buttons.Count);
        foreach (var button in buttons)
        {
            var interaction = Interaction.Register(
                userId, button.OwnerModule, button.Action, button.Payload, notification.Id, expiresAt, timeProvider);
            await interactions.AddAsync(interaction, ct);
            rendered.Add(new TelegramRenderedButton(interaction.Id.ToString(), button.Label));
        }

        // The notification is already change-tracked from AddAsync above; mutating it before the unit
        // of work commits is enough. Calling UpdateAsync here would flip its state from Added to
        // Modified and turn the pending INSERT into an UPDATE of a row that does not exist yet.
        notification.SetRenderedPayload(new TelegramRenderedPayload(text, rendered).Serialize());
    }
}
