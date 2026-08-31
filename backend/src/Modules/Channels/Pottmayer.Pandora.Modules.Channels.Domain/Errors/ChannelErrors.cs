using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Channels.Domain.Errors;

public static class ChannelErrors
{
    public static Error UnsupportedChannel(string channel) =>
        Error.Validation("Channels.UnsupportedChannel", $"Channel '{channel}' is not supported.");

    public static Error LinkNotSupported(string channel) =>
        Error.Validation("Channels.LinkNotSupported", $"Channel '{channel}' is not linked by the user.");

    public static Error NotLinked =>
        Error.NotFound("Channels.NotLinked", "This channel is not linked for the user.");

    public static Error NotUsable =>
        Error.Validation("Channels.NotUsable", "This channel is not verified or has been disabled.");

    public static Error TelegramNotConfigured =>
        Error.Validation("Channels.TelegramNotConfigured", "Telegram is not configured on this server.");

    public static Error LinkTokenInvalid =>
        Error.Validation("Channels.LinkTokenInvalid", "This link is invalid or has expired. Start linking again from settings.");

    public static Error InvalidQuietHoursBehaviour(string behaviour) =>
        Error.Validation("Channels.InvalidQuietHoursBehaviour", $"Quiet-hours behaviour '{behaviour}' is not supported.");

    public static Error InvalidQuietHoursWindow =>
        Error.Validation("Channels.InvalidQuietHoursWindow", "Quiet hours must have a start and end time, and they cannot be equal.");
}
