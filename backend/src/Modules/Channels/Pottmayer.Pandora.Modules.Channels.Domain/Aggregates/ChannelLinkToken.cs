using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;

/// <summary>
/// The handshake that ties a chat to an account. Single use and short lived: it is the only thing
/// that authorizes an address, which is never accepted from the client.
/// </summary>
/// <remarks>
/// Only the SHA-256 of the token is stored; the plaintext travels in the deep link and nowhere else,
/// the same way Identity treats activation and reset tokens.
/// </remarks>
public sealed class ChannelLinkToken : AggregateRoot<Guid>, IAuditable
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(15);

    public Guid UserId { get; private set; }
    public Channel Channel { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public string Locale { get; private set; } = "en";
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private ChannelLinkToken() { }

    public static ChannelLinkToken Issue(
        Guid userId,
        Channel channel,
        string tokenHash,
        string locale,
        TimeProvider timeProvider,
        TimeSpan? lifetime = null)
    {
        var now = timeProvider.GetUtcNow();
        return new ChannelLinkToken
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Channel = channel,
            TokenHash = tokenHash,
            Locale = locale,
            ExpiresAt = now + (lifetime ?? DefaultLifetime),
            CreatedAt = now
        };
    }

    public bool IsUsable(DateTimeOffset now) => ConsumedAt is null && ExpiresAt > now;

    /// <summary>Burns the token. A second start with the same code is rejected, not replayed.</summary>
    public void Consume(TimeProvider timeProvider) => ConsumedAt = timeProvider.GetUtcNow();
}
