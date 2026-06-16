using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface ICardStatementRepository : IStandardRepository<CardStatement, Guid>
{
    Task<CardStatement?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<CardStatement?> FindByCardAndReferenceMonthAsync(Guid cardId, Guid userId, string referenceMonth, CancellationToken ct = default);
    Task<IReadOnlyList<CardStatement>> GetByCardAsync(Guid cardId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<CardStatement>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<CardStatement>> GetLifecycleCandidatesAsync(DateOnly today, CancellationToken ct = default);
}
