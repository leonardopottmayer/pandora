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

    /// <summary>Retention policy for the raw inbound update payload stored in <c>chn004.raw</c>.</summary>
    public RawRetentionOptions RawRetention { get; set; } = new();
}

/// <summary>
/// Controls purging of the raw inbound update payload (<c>chn004.raw</c>). The row is never deleted —
/// it is the idempotency guard and the long-polling offset — only the raw JSON is cleared once it
/// ages out, because it is personal data (message text, and eventually voice transcripts) kept only
/// for debugging.
/// </summary>
public sealed class RawRetentionOptions
{
    /// <summary>
    /// Whether the retention job runs. Off leaves raw payloads in place indefinitely — useful while
    /// debugging inbound, but it keeps personal data around.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How many days a raw payload is kept before it is cleared. Minimum one.</summary>
    public int RetentionDays { get; set; } = 7;
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

    /// <summary>
    /// Whether to pull inbound updates by long polling. Off by default: it needs a bot token and, being
    /// a singleton consumer, must not run in a second replica against the same bot. The webhook is the
    /// eventual alternative and needs public HTTPS, which the homelab does not expose.
    /// </summary>
    public bool LongPolling { get; set; }

    /// <summary>How long each <c>getUpdates</c> call hangs waiting for an update, in seconds.</summary>
    public int PollTimeoutSeconds { get; set; } = 30;
}
