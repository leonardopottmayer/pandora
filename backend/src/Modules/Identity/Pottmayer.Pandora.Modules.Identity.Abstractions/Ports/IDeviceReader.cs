using Pottmayer.Pandora.Modules.Identity.Abstractions.Models;

namespace Pottmayer.Pandora.Modules.Identity.Abstractions.Ports;

/// <summary>
/// The question Identity answers for modules that configure work per device (e.g. Files roots):
/// "is <c>D</c> one of user <c>U</c>'s paired, non-revoked devices, and what is it?"
/// </summary>
public interface IDeviceReader
{
    /// <summary>The device, or <c>null</c> when it does not exist, is revoked or belongs to someone else.</summary>
    Task<DeviceSnapshot?> GetActiveAsync(Guid userId, Guid deviceId, CancellationToken ct = default);
}
