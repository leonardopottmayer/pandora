using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface IUserCategoryRepository : IStandardRepository<UserCategory, Guid>
{
    /// <summary>One category owned by the user, or <c>null</c> (used for the 404-on-foreign-resource rule).</summary>
    Task<UserCategory?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>All of the user's categories, ordered for tree assembly.</summary>
    Task<IReadOnlyList<UserCategory>> GetAllForUserAsync(
        Guid userId, bool includeInactive, CancellationToken ct = default);

    /// <summary>Whether the user already has a category with this name under the given parent.</summary>
    Task<bool> ExistsWithNameAsync(
        Guid userId, string name, Guid? parentCategoryId, Guid? excludingId, CancellationToken ct = default);
}
