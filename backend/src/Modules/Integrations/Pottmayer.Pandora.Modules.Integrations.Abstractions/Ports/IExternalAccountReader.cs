using Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;

namespace Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;

/// <summary>
/// Read access to connected accounts for other modules. Returns summaries only — no tokens ever
/// cross this boundary.
/// </summary>
public interface IExternalAccountReader
{
    /// <summary>The user's accounts, optionally only those of <paramref name="provider"/>.</summary>
    Task<IReadOnlyList<ExternalAccountSummary>> ListAsync(
        Guid userId, string? provider = null, CancellationToken ct = default);

    /// <summary>One account, or null when it does not exist or belongs to someone else.</summary>
    Task<ExternalAccountSummary?> GetAsync(Guid userId, Guid externalAccountId, CancellationToken ct = default);
}
