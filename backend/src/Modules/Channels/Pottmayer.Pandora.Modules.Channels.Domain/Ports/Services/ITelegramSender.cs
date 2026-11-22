using Pottmayer.Pandora.Modules.Channels.Domain.Rendering;

namespace Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;

/// <summary>
/// Sends a text message through a named Telegram bot to a chat — the direct, interactive counterpart
/// to the notification dispatcher, with no template, queue or retry. Implementations throw on a delivery
/// failure; the caller decides whether that is worth surfacing.
/// </summary>
/// <remarks>
/// An internal seam, not a module boundary: the bot name matches a <c>Tars:Communication:Telegram:Bots</c>
/// key, and the implementation lives in this module's Infrastructure over the Tars client factory.
/// </remarks>
public interface ITelegramSender
{
    /// <summary>Sends <paramref name="text"/>, with one inline button per row when <paramref name="buttons"/> is non-empty.</summary>
    Task SendAsync(
        string bot, string chatId, string text,
        IReadOnlyList<TelegramRenderedButton>? buttons = null, CancellationToken ct = default);
}
