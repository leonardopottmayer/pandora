using Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Integrations.Domain.Ports.Repositories;

public interface IExternalAccountRepository : IStandardRepository<ExternalAccount, Guid>
{
    /// <summary>Finds the account for a user at a provider. One connected account per provider in the MVP.</summary>
    Task<ExternalAccount?> FindAsync(Guid userId, string provider, CancellationToken ct = default);

    /// <summary>Every connected account of a user, for the settings screen and the account reader.</summary>
    Task<IReadOnlyList<ExternalAccount>> GetByUserAsync(Guid userId, CancellationToken ct = default);
}
