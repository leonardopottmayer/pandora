namespace Pottmayer.Pandora.Modules.Channels.Abstractions;

/// <summary>
/// Configuration for the Channels module (bound from the <c>Channels</c> section).
/// </summary>
public sealed class ChannelsOptions
{
    public const string SectionName = "Pandora:Channels";

    /// <summary>URL template for activation links; <c>{token}</c> is replaced at render time.</summary>
    public string ActivationUrlTemplate { get; set; } = "https://localhost/activate?token={token}";

    /// <summary>URL template for password reset links; <c>{token}</c> is replaced at render time.</summary>
    public string PasswordResetUrlTemplate { get; set; } = "https://localhost/reset-password?token={token}";

    /// <summary>How often the dispatcher worker drains the queue.</summary>
    public int DispatchIntervalSeconds { get; set; } = 15;

    /// <summary>How many notifications the worker processes per tick.</summary>
    public int DispatchBatchSize { get; set; } = 20;

    /// <summary>Telegram-specific settings. Empty <see cref="TelegramChannelOptions.BotUsername"/> disables the channel.</summary>
    public TelegramChannelOptions Telegram { get; set; } = new();
}

/// <summary>
/// What this module needs to know about the bot, as opposed to how to talk to it — the token and the
/// HTTP details belong to the Tars building block.
/// </summary>
public sealed class TelegramChannelOptions
{
    /// <summary>
    /// The bot's public username, without the leading @. Used to build the deep link the user taps.
    /// Empty means Telegram is not configured, and linking is refused instead of half-working.
    /// </summary>
    public string BotUsername { get; set; } = string.Empty;
}
