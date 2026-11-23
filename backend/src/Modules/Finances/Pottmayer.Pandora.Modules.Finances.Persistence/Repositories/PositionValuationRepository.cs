using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class PositionValuationRepository(IDataContextAccessor accessor)
    : StandardRepository<PositionValuation, Guid>(accessor), IPositionValuationRepository
{
    public Task<PositionValuation?> FindOnDateAsync(Guid positionId, DateOnly valuedOn, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(v => v.PositionId == positionId && v.ValuedOn == valuedOn, ct);

    public async Task<IReadOnlyList<PositionValuation>> ListByPositionAsync(Guid positionId, CancellationToken ct = default)
        => await Queryable().Where(v => v.PositionId == positionId).OrderByDescending(v => v.ValuedOn).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, PositionValuation>> GetLatestAsync(
        IReadOnlyCollection<Guid> positionIds, CancellationToken ct = default)
    {
        if (positionIds.Count == 0) return new Dictionary<Guid, PositionValuation>();

        // ponytail: loads every value of the given positions and keeps the newest in memory; a
        // DISTINCT ON query when the history grows large.
        var values = await Queryable().Where(v => positionIds.Contains(v.PositionId)).ToListAsync(ct);
        return values
            .GroupBy(v => v.PositionId)
            .ToDictionary(g => g.Key, g => g.MaxBy(v => v.ValuedOn)!);
    }
}
