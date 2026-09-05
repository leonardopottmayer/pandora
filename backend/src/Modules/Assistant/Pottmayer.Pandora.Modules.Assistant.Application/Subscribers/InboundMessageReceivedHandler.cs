using Microsoft.Extensions.Logging;
using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Application.Commands.Interpret;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Subscribers;

/// <summary>
/// The Telegram side of the assistant: an inbound message that arrived on the assistant bot is interpreted
/// exactly like the web command bar, and whatever it produced is sent back to the user through Channels.
/// Runs one message at a time as an integration-event subscriber — the latency and durability the async
/// path was meant to give — and speaks only in domain terms, never touching the Bot API.
/// </summary>
/// <remarks>
/// Only the assistant bot's inbound is a conversation; messages on other bots (e.g. notifications) are not
/// this handler's to interpret. The conversation is resolved channel-agnostically inside the interpret
/// pipeline (no conversation id → the user's most recent non-expired thread), so a follow-up typed on
/// Telegram continues one begun on the web, and vice versa.
/// </remarks>
public sealed class InboundMessageReceivedHandler(
    IUnitOfWorkFactory factory,
    ISender sender,
    IIntegrationEventBus bus,
    TimeProvider timeProvider,
    ILogger<InboundMessageReceivedHandler> logger)
    : IIntegrationEventHandler<InboundMessageReceived>
{
    public async Task HandleAsync(InboundMessageReceived @event, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(@event.Bot, AssistantModule.Name, StringComparison.OrdinalIgnoreCase))
            return;

        // Media-only messages carry no text to interpret yet — voice is A4.
        if (string.IsNullOrWhiteSpace(@event.Text))
            return;

        var result = await sender.Send(new InterpretCommand(new InterpretInput(@event.UserId, @event.Text)), cancellationToken);
        if (!result.IsSuccess)
        {
            // Pre-flight failures only reach here (assistant disabled, no API key): nothing to say to the
            // user that would not leak configuration. The web settings screen is where they fix it.
            logger.LogWarning("Interpret rejected an inbound assistant message from user {UserId}.", @event.UserId);
            return;
        }

        var reply = result.Value.Message;
        if (string.IsNullOrWhiteSpace(reply))
            return;

        // Ask Channels to deliver the reply on the assistant bot. Published in a unit of work so it rides
        // the transactional outbox rather than being lost on a crash after the interpret committed.
        await factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, token) =>
        {
            await bus.PublishAsync(
                new SendAssistantReply(Guid.CreateVersion7(), timeProvider.GetUtcNow(), @event.UserId, AssistantModule.Name, reply),
                token);
            return true;
        }, cancellationToken: cancellationToken);
    }
}
