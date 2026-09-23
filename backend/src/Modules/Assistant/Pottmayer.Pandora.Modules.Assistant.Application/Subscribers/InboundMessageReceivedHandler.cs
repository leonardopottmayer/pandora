using Microsoft.Extensions.Logging;
using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Application.Commands.Interpret;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Ai.Chat.Abstractions.Models;
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
/// Telegram continues one begun on the web, and vice versa. A voice note is downloaded through Channels'
/// <see cref="IInboundMediaReader"/> and handed to the pipeline to transcribe; the reply echoes what was
/// heard.
/// </remarks>
public sealed class InboundMessageReceivedHandler(
    IUnitOfWorkFactory factory,
    ISender sender,
    IIntegrationEventBus bus,
    IInboundMediaReader media,
    TimeProvider timeProvider,
    ILogger<InboundMessageReceivedHandler> logger)
    : IIntegrationEventHandler<InboundMessageReceived>
{
    /// <summary>Several minutes of Opus voice; well under the provider's inline-request limit.</summary>
    private const int MaxAudioBytes = 5 * 1024 * 1024;

    public async Task HandleAsync(InboundMessageReceived @event, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(@event.Bot, AssistantModule.Name, StringComparison.OrdinalIgnoreCase))
            return;

        ChatAttachment? audio = null;
        if (string.IsNullOrWhiteSpace(@event.Text))
        {
            // Only voice/audio is understood without text; photos and documents are not handled yet.
            if (@event.MediaRef is null || @event.MediaMimeType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) != true)
                return;

            audio = await ReadAudioAsync(@event, cancellationToken);
            if (audio is null)
            {
                await ReplyAsync(@event.UserId, "That voice note is too long. Keep it to a few minutes.", cancellationToken);
                return;
            }
        }

        var result = await sender.Send(
            new InterpretCommand(new InterpretInput(@event.UserId, @event.Text, Audio: audio)), cancellationToken);
        if (!result.IsSuccess)
        {
            // Pre-flight failures only reach here (assistant disabled, no API key): nothing to say to the
            // user that would not leak configuration. The web settings screen is where they fix it.
            logger.LogWarning("Interpret rejected an inbound assistant message from user {UserId}.", @event.UserId);
            return;
        }

        var reply = result.Value.Message;
        if (result.Value.Transcript is { } heard)
            reply = $"🎤 “{heard}”\n\n{reply}";

        if (string.IsNullOrWhiteSpace(reply))
            return;

        await ReplyAsync(@event.UserId, reply, cancellationToken);
    }

    /// <summary>Reads the voice note into memory, or null when it exceeds <see cref="MaxAudioBytes"/>.</summary>
    private async Task<ChatAttachment?> ReadAudioAsync(InboundMessageReceived @event, CancellationToken ct)
    {
        await using var stream = await media.OpenAsync(@event.Channel, @event.Bot, @event.MediaRef!, ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxAudioBytes)
                return null;
            buffer.Write(chunk, 0, read);
        }

        return new ChatAttachment(buffer.ToArray(), @event.MediaMimeType!);
    }

    /// <summary>
    /// Asks Channels to deliver a reply on the assistant bot. Published in a unit of work so it rides the
    /// transactional outbox rather than being lost on a crash after the interpret committed.
    /// </summary>
    private Task ReplyAsync(Guid userId, string text, CancellationToken ct) =>
        factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, token) =>
        {
            await bus.PublishAsync(
                new SendAssistantReply(Guid.CreateVersion7(), timeProvider.GetUtcNow(), userId, AssistantModule.Name, text),
                token);
            return true;
        }, cancellationToken: ct);
}
