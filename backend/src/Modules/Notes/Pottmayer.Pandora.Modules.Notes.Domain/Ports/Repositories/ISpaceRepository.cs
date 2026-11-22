using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;

public interface ISpaceRepository : IStandardRepository<Space, Guid>
{
    /// <summary>One non-deleted space owned by the user, or <c>null</c> (404-on-foreign-resource rule).</summary>
    Task<Space?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>The user's non-deleted spaces, the default first, then by name.</summary>
    Task<IReadOnlyList<Space>> GetForUserAsync(Guid userId, bool includeArchived, CancellationToken ct = default);

    /// <summary>The user's default space, or <c>null</c> when they have none yet.</summary>
    Task<Space?> FindDefaultAsync(Guid userId, CancellationToken ct = default);
}
