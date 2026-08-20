using System.Globalization;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Communication.Telegram.Abstractions;
using Pottmayer.Tars.Communication.Telegram.Abstractions.Models;

namespace Pottmayer.Pandora.Modules.Channels.Infrastructure.Transports;

/// <summary>
/// Delivers over the Telegram Bot API through the Tars client. The address is the numeric chat id
/// captured when the user linked their account.
/// </summary>
/// <remarks>
/// A permanent Bot API failure — the chat is gone, the bot was blocked — is turned into a
/// <see cref="PermanentDeliveryException"/> so the dispatcher kills the row and disables the channel,
/// instead of burning retries against a wall. Everything else bubbles up as transient and is retried
/// with backoff. The message goes out as plain text today; structured content (parse mode, inline
/// buttons) arrives with the fan-out and interaction work, once something produces
/// <see cref="Notification.RenderedPayload"/>.
/// </remarks>
public sealed class TelegramChannelTransport(ITelegramClient client) : IChannelTransport
{
    private const string ProviderName = "telegram";

    public Channel Channel => Channel.Telegram;

    public async Task<ChannelDeliveryResult> SendAsync(Notification notification, CancellationToken ct = default)
    {
        var message = new TelegramMessage(
            ChatId: notification.Address.Value,
            Text: notification.Body);

        try
        {
            var result = await client.SendMessageAsync(message, ct);
            return new ChannelDeliveryResult(
                ProviderName, result.MessageId.ToString(CultureInfo.InvariantCulture));
        }
        catch (TelegramException ex) when (ex.IsPermanent)
        {
            throw new PermanentDeliveryException(ex.Message, ex);
        }
    }
}
