using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;

/// <summary>
/// Delivery channel for a notification. Smart enum: Sms / WhatsApp can be added later without
/// touching callers.
/// </summary>
public sealed class Channel : IDomainValue<Channel>
{
    public static readonly Channel Email = new("email");
    public static readonly Channel Telegram = new("telegram");

    public string Value { get; }

    private Channel(string value) => Value = value;

    public static Channel FromValue(string value) => value switch
    {
        "email" => Email,
        "telegram" => Telegram,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown notification channel.")
    };

    /// <summary>Parses without throwing, for values that arrive from outside (a route, a request body).</summary>
    public static bool TryFromValue(string? value, out Channel channel)
    {
        switch (value?.ToLowerInvariant())
        {
            case "email":
                channel = Email;
                return true;
            case "telegram":
                channel = Telegram;
                return true;
            default:
                channel = Email;
                return false;
        }
    }

    public override string ToString() => Value;
}
