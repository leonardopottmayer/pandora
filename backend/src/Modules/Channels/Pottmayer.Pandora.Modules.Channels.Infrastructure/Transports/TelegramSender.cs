using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Channels.Domain.Rendering;
using Pottmayer.Tars.Communication.Telegram.Abstractions;
using Pottmayer.Tars.Communication.Telegram.Abstractions.Models;

namespace Pottmayer.Pandora.Modules.Channels.Infrastructure.Transports;

/// <summary>
/// Sends a message (optionally with inline buttons) through a named bot resolved from the Tars factory. The bot name is a
/// <c>Tars:Communication:Telegram:Bots</c> key (e.g. <c>assistant</c>).
/// </summary>
public sealed class TelegramSender(ITelegramClientFactory telegram) : ITelegramSender
{
    public Task SendAsync(
        string bot, string chatId, string text,
        IReadOnlyList<TelegramRenderedButton>? buttons = null, CancellationToken ct = default)
    {
        var keyboard = buttons is { Count: > 0 }
            ? InlineKeyboard.Stacked(buttons.Select(b => InlineButton.Callback(b.Label, b.InteractionId)).ToArray())
            : null;
        return telegram.GetClient(bot).SendMessageAsync(new TelegramMessage(chatId, text, Keyboard: keyboard), ct);
    }
}
