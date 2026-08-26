namespace Pottmayer.Pandora.Modules.Integrations.Domain.Ports;

/// <summary>
/// A provider call failed. <see cref="IsPermanent"/> distinguishes a rejected grant
/// (<c>invalid_grant</c> — reconnect required, do not retry) from a transient transport error
/// (retry later).
/// </summary>
public sealed class OAuthException(string message, bool isPermanent, Exception? inner = null)
    : Exception(message, inner)
{
    public bool IsPermanent { get; } = isPermanent;
}
