using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class CardNumberRepository(IDataContextAccessor accessor)
    : StandardRepository<CardNumber, Guid>(accessor), ICardNumberRepository
{
    public Task<CardNumber?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);

    public async Task<IReadOnlyList<CardNumber>> GetByCardsAsync(
        IReadOnlyCollection<Guid> cardIds, Guid userId, CancellationToken ct = default)
    {
        if (cardIds.Count == 0) return [];

        var numbers = await Queryable()
            .Where(n => n.UserId == userId && cardIds.Contains(n.CardId))
            .ToListAsync(ct);

        return [.. numbers
            .OrderBy(n => n.Kind == CardNumberKind.Physical ? 0 : 1)
            .ThenBy(n => n.CreatedAt)];
    }

    public Task<bool> ExistsWithLastFourAsync(Guid cardId, string lastFour, Guid? excludingId, CancellationToken ct = default)
    {
        var trimmed = lastFour.Trim();
        var query = Queryable().Where(n => n.CardId == cardId && n.LastFour == trimmed);

        if (excludingId is not null)
            query = query.Where(n => n.Id != excludingId);

        return query.AnyAsync(ct);
    }
}
