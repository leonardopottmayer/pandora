using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface IAccountRepository : IStandardRepository<Account, Guid>
{
    /// <summary>One account owned by the user, or <c>null</c> (404-on-foreign-resource rule).</summary>
    Task<Account?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>The user's accounts, ordered by display order then name; archived ones optional.</summary>
    Task<IReadOnlyList<Account>> GetAllForUserAsync(
        Guid userId, bool includeArchived, CancellationToken ct = default);

    /// <summary>Whether the user already has an account with this name (case-insensitive).</summary>
    Task<bool> ExistsWithNameAsync(Guid userId, string name, Guid? excludingId, CancellationToken ct = default);
}
