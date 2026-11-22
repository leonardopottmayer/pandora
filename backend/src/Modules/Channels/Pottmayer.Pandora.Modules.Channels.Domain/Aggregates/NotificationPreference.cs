using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;

/// <summary>
/// A user's delivery policy for one category of notification: the ordered set of channels it goes
/// out on. An empty set is an explicit mute. The absence of a row is not a mute — it means "no
/// choice made", and the fan-out falls back to a default.
/// </summary>
public sealed class NotificationPreference : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public string Category { get; private set; } = string.Empty;

    /// <summary>Channel values, in the order the user ranked them. Empty means muted.</summary>
    public List<string> Channels { get; private set; } = [];

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private NotificationPreference() { }

    public static NotificationPreference Create(
        Guid userId, string category, IEnumerable<Channel> channels, TimeProvider timeProvider)
    {
        var preference = new NotificationPreference
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Category = category,
            CreatedAt = timeProvider.GetUtcNow()
        };
        preference.SetChannels(channels);
        return preference;
    }

    /// <summary>Replaces the channel set, dropping duplicates while keeping the given order.</summary>
    public void SetChannels(IEnumerable<Channel> channels)
    {
        var values = new List<string>();
        foreach (var channel in channels)
        {
            if (!values.Contains(channel.Value))
                values.Add(channel.Value);
        }

        Channels = values;
    }

    /// <summary>The stored channels as value objects, in order.</summary>
    public IReadOnlyList<Channel> ResolvedChannels() => [.. Channels.Select(Channel.FromValue)];
}
