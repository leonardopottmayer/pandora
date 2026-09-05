using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;
using Pottmayer.Tars.Communication.Telegram.Abstractions;
using Pottmayer.Tars.Communication.Telegram.Abstractions.Models;

namespace Pottmayer.Pandora.Modules.Channels.Infrastructure.Transports;

/// <summary>
/// Sends a plain message through a named bot resolved from the Tars factory. The bot name is a
/// <c>Tars:Communication:Telegram:Bots</c> key (e.g. <c>assistant</c>).
/// </summary>
public sealed class TelegramSender(ITelegramClientFactory telegram) : ITelegramSender
{
    public Task SendAsync(string bot, string chatId, string text, CancellationToken ct = default) =>
        telegram.GetClient(bot).SendMessageAsync(new TelegramMessage(chatId, text), ct);
}
