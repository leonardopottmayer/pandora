using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;

/// <summary>
/// Where a notification is delivered. The invariant is channel-specific: an e-mail address is
/// validated by the shared <see cref="Email"/> value object, a Telegram address is a chat id.
/// The channel itself lives on the notification, not here — one column, one value.
/// </summary>
public sealed record NotificationAddress : IDomainValue<NotificationAddress>
{
    public string Value { get; }

    private NotificationAddress(string value) => Value = value;

    public static NotificationAddress Create(Channel channel, string raw)
    {
        if (channel == Channel.Email)
            return new NotificationAddress(Email.Create(raw).Value);

        if (channel == Channel.Telegram)
            return long.TryParse(raw, out _)
                ? new NotificationAddress(raw)
                : throw new ArgumentException($"'{raw}' is not a valid Telegram chat id.", nameof(raw));

        throw new ArgumentOutOfRangeException(nameof(channel), channel.Value, "Unknown notification channel.");
    }

    /// <summary>Rehydration from the database. The invariant was enforced on the way in.</summary>
    public static NotificationAddress FromValue(string value) => new(value);

    public override string ToString() => Value;
}
