using Pottmayer.Pandora.Modules.Integrations.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Integrations.Domain.Ports.Repositories;

public interface IExternalAccountRepository : IStandardRepository<ExternalAccount, Guid>
{
    /// <summary>Finds one account by its identity at the provider (Google <c>sub</c>, mailbox username).</summary>
    Task<ExternalAccount?> FindAsync(Guid userId, string provider, string providerAccountId, CancellationToken ct = default);

    /// <summary>Loads an account only when it belongs to <paramref name="userId"/>; null otherwise.</summary>
    Task<ExternalAccount?> GetOwnedAsync(Guid userId, Guid accountId, CancellationToken ct = default);

    /// <summary>Every connected account of a user, for the settings screen and the account reader.</summary>
    Task<IReadOnlyList<ExternalAccount>> GetByUserAsync(Guid userId, CancellationToken ct = default);
}
