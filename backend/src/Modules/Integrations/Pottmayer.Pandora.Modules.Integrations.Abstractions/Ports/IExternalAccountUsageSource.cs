using Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;

namespace Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;

/// <summary>
/// Implemented by every module that binds to connected accounts, so the connections screen can show
/// "used by" and warn before a disconnect. Integrations collects every registration through DI and
/// references no consumer: the dependency keeps pointing at this module.
/// </summary>
public interface IExternalAccountUsageSource
{
    /// <summary>Every place this module uses one of <paramref name="userId"/>'s accounts.</summary>
    Task<IReadOnlyList<ExternalAccountUsage>> ListAsync(Guid userId, CancellationToken ct = default);
}
