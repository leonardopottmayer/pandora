using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface ICardRepository : IStandardRepository<Card, Guid>
{
    Task<Card?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Card>> GetAllForUserAsync(Guid userId, bool includeArchived, CancellationToken ct = default);
    Task<IReadOnlyList<Card>> GetAllActiveAsync(CancellationToken ct = default);
    Task<bool> ExistsWithNameAsync(Guid userId, string name, Guid? excludingId, CancellationToken ct = default);
}
