using Pottmayer.Pandora.Shared.Domain;

namespace Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;

/// <summary>
/// Delivery channel for a notification. Smart enum: only <see cref="Email"/> exists now;
/// Sms / Telegram / WhatsApp can be added later without touching callers.
/// </summary>
public sealed class Channel : IDomainValue<Channel>
{
    public static readonly Channel Email = new("email");

    public string Value { get; }

    private Channel(string value) => Value = value;

    public static Channel FromValue(string value) => value switch
    {
        "email" => Email,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown notification channel.")
    };

    public override string ToString() => Value;
}
