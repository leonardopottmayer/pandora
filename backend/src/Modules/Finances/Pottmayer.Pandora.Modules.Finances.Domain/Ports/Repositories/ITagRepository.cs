using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface ITagRepository : IStandardRepository<Tag, Guid>
{
    /// <summary>One tag owned by the user, or <c>null</c> (used for the 404-on-foreign-resource rule).</summary>
    Task<Tag?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>All of the user's tags, ordered by name.</summary>
    Task<IReadOnlyList<Tag>> GetAllForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>The user's tags whose ids are in <paramref name="ids"/>, ordered by name.</summary>
    Task<IReadOnlyList<Tag>> GetByIdsForUserAsync(Guid userId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>Whether the user already has a tag with this name (case-insensitive).</summary>
    Task<bool> ExistsWithNameAsync(Guid userId, string name, Guid? excludingId, CancellationToken ct = default);
}
