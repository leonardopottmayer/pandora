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

    public override string ToString() => Value;
}
