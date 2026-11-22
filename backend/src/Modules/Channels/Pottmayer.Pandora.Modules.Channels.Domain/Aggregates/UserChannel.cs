using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;

/// <summary>
/// Where a user can be reached on one channel. An address is only usable once it is both verified
/// (a handshake proved the user owns it) and enabled (neither the user nor a permanent provider
/// failure has switched it off).
/// </summary>
public sealed class UserChannel : AggregateRoot<Guid>, IAuditable
{
    private const int MaxReasonLength = 500;

    public Guid UserId { get; private set; }
    public Channel Channel { get; private set; } = null!;
    public NotificationAddress Address { get; private set; } = null!;
    public string Locale { get; private set; } = "en";
    public bool IsVerified { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public bool IsEnabled { get; private set; }
    public string? DisabledReason { get; private set; }
    public string Metadata { get; private set; } = "{}";

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private UserChannel() { }

    /// <summary>
    /// Records a verified address. Verification is proof the caller already has, not a promise: only
    /// a consumed link token may reach this.
    /// </summary>
    public static UserChannel LinkVerified(
        Guid userId,
        Channel channel,
        NotificationAddress address,
        string locale,
        string metadata,
        TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        return new UserChannel
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Channel = channel,
            Address = address,
            Locale = locale,
            IsVerified = true,
            VerifiedAt = now,
            IsEnabled = true,
            Metadata = metadata,
            CreatedAt = now
        };
    }

    /// <summary>Whether the dispatcher may deliver to this address.</summary>
    public bool IsUsable => IsVerified && IsEnabled;

    /// <summary>
    /// Points an existing link at a new address, which is what re-running the handshake from another
    /// chat means. Clears a previous disable too, since the user just proved the channel works.
    /// </summary>
    public void Relink(NotificationAddress address, string locale, string metadata, TimeProvider timeProvider)
    {
        Address = address;
        Locale = locale;
        Metadata = metadata;
        IsVerified = true;
        VerifiedAt = timeProvider.GetUtcNow();
        IsEnabled = true;
        DisabledReason = null;
    }

    /// <summary>
    /// Stops delivery to this address. Used both by the user and by a permanent provider failure:
    /// there is no point retrying a blocked bot, and the user has to learn that it stopped.
    /// </summary>
    public void Disable(string reason)
    {
        IsEnabled = false;
        DisabledReason = reason.Length <= MaxReasonLength ? reason : reason[..MaxReasonLength];
    }
}
