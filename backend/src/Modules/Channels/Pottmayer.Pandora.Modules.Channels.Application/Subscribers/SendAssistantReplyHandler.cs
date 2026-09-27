using Microsoft.Extensions.Logging;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Channels.Domain.Rendering;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Application.Subscribers;

/// <summary>
/// Delivers the assistant's reply to the user on the bot they are talking to. Resolves the user's Telegram
/// chat from their linked channel — the chat id is the same across bots — and sends best-effort: a failed
/// reply is logged, never retried, so it cannot poison the event or double-send. Buttons are registered as
/// interactions first, so a tap resolves through the same single-use callback path as a notification's.
/// </summary>
public sealed class SendAssistantReplyHandler(
    IUnitOfWorkFactory factory,
    ITelegramSender sender,
    TimeProvider timeProvider,
    ILogger<SendAssistantReplyHandler> logger)
    : IIntegrationEventHandler<SendAssistantReply>
{
    public async Task HandleAsync(SendAssistantReply @event, CancellationToken cancellationToken = default)
    {
        var (address, buttons) = await factory.ExecuteAsync(ChannelsModule.DatabaseKey, async (context, token) =>
        {
            var channels = context.AcquireRepository<IUserChannelRepository>();
            var link = await channels.FindAsync(@event.UserId, Channel.Telegram, token);
            if (link is null)
                return (null, []);

            var rendered = new List<TelegramRenderedButton>();
            if (@event.Buttons is { Count: > 0 })
            {
                var interactions = context.AcquireRepository<IInteractionRepository>();
                var expiresAt = timeProvider.GetUtcNow() + Interaction.Lifetime;
                foreach (var button in @event.Buttons)
                {
                    var interaction = Interaction.Register(
                        @event.UserId, button.OwnerModule, button.Action, button.Payload,
                        notificationId: null, expiresAt, timeProvider);
                    await interactions.AddAsync(interaction, token);
                    rendered.Add(new TelegramRenderedButton(interaction.Id.ToString(), button.Label));
                }
            }

            return ((string?)link.Address.Value, (IReadOnlyList<TelegramRenderedButton>)rendered);
        }, cancellationToken: cancellationToken);

        if (address is null)
        {
            logger.LogWarning("No Telegram chat linked for user {UserId}; dropping assistant reply.", @event.UserId);
            return;
        }

        try
        {
            await sender.SendAsync(@event.Bot, address, @event.Text, buttons, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort: a failed interactive reply is logged, not retried — retrying risks a
            // double-send once a transient error clears, and the user can simply ask again.
            logger.LogWarning(ex, "Failed to send assistant reply to user {UserId} via bot {Bot}.", @event.UserId, @event.Bot);
        }
    }
}
