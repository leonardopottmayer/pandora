using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface ICardNumberRepository : IStandardRepository<CardNumber, Guid>
{
    Task<CardNumber?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>The numbers of the given cards, archived ones included, physical first.</summary>
    Task<IReadOnlyList<CardNumber>> GetByCardsAsync(IReadOnlyCollection<Guid> cardIds, Guid userId, CancellationToken ct = default);

    Task<bool> ExistsWithLastFourAsync(Guid cardId, string lastFour, Guid? excludingId, CancellationToken ct = default);
}
