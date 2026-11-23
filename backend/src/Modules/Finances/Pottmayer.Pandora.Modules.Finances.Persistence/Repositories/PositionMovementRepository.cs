using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class PositionMovementRepository(IDataContextAccessor accessor)
    : StandardRepository<PositionMovement, Guid>(accessor), IPositionMovementRepository
{
    // Ordered by creation, not by occurred_on: undo follows the order the movements were recorded in,
    // which is the order their *_before snapshots chain.
    public async Task<IReadOnlyList<PositionMovement>> ListByPositionAsync(Guid positionId, CancellationToken ct = default)
        => await Queryable().Where(m => m.PositionId == positionId).OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToListAsync(ct);

    public Task<PositionMovement?> FindLatestAsync(Guid positionId, CancellationToken ct = default)
        => Queryable().Where(m => m.PositionId == positionId)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .FirstOrDefaultAsync(ct);

    public Task<bool> AnyForPositionAsync(Guid positionId, CancellationToken ct = default)
        => Queryable().AnyAsync(m => m.PositionId == positionId, ct);

    public Task<bool> ExistsForTransactionAsync(Guid transactionId, CancellationToken ct = default)
        => Queryable().AnyAsync(m => m.TransactionId == transactionId, ct);
}
