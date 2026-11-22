namespace Pottmayer.Pandora.Modules.Channels.Infrastructure;

/// <summary>
/// Names of the Telegram bots Channels resolves from the Tars <c>ITelegramClientFactory</c>. Each maps to
/// a configuration key under <c>Tars:Communication:Telegram:Bots</c>.
/// </summary>
internal static class TelegramBots
{
    /// <summary>The bot Channels sends notifications through and long-polls for inbound.</summary>
    internal const string Notifications = "notifications";
}
