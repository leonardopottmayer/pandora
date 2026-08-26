using Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;

namespace Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;

/// <summary>
/// Read access to connected accounts for other modules. Returns summaries only — no tokens ever
/// cross this boundary.
/// </summary>
public interface IExternalAccountReader
{
    Task<IReadOnlyList<ExternalAccountSummary>> ListAsync(Guid userId, CancellationToken ct = default);

    Task<ExternalAccountSummary?> GetAsync(Guid externalAccountId, CancellationToken ct = default);
}
