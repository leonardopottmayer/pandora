using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;

/// <summary>
/// Delivers one already-rendered notification over one channel. Adding WhatsApp is a new
/// implementation and a new template variant, never a branch in the dispatcher.
/// </summary>
/// <remarks>
/// This is an internal seam, not a module boundary: it has exactly one caller, the dispatcher, and
/// the implementations live in this module's Infrastructure.
/// </remarks>
public interface IChannelTransport
{
    /// <summary>The channel this transport serves. The dispatcher selects on it.</summary>
    Channel Channel { get; }

    /// <summary>
    /// Hands the notification to the provider.
    /// </summary>
    /// <exception cref="PermanentDeliveryException">
    /// The failure will repeat forever: the chat is gone, the bot is blocked, the address is refused.
    /// </exception>
    /// <exception cref="Exception">Any other failure is transient and worth retrying.</exception>
    Task<ChannelDeliveryResult> SendAsync(Notification notification, CancellationToken ct = default);
}

/// <summary>Outcome of a successful delivery: who accepted it, and their id for it.</summary>
public sealed record ChannelDeliveryResult(string Provider, string? ProviderMessageId);

/// <summary>
/// A delivery failure that retrying cannot fix. The dispatcher answers it by killing the
/// notification and switching the address off, instead of burning five attempts against a wall.
/// </summary>
public sealed class PermanentDeliveryException(string message, Exception? innerException = null)
    : Exception(message, innerException);
