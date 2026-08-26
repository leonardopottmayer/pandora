using Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Integrations.Domain.Ports.Repositories;

public interface IOAuthStateRepository : IStandardRepository<OAuthState, Guid>
{
    /// <summary>Looks up an in-flight request by its CSRF state token.</summary>
    Task<OAuthState?> FindByStateAsync(string state, CancellationToken ct = default);
}
