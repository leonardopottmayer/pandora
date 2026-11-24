using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface IPositionMovementRepository : IStandardRepository<PositionMovement, Guid>
{
    /// <summary>A position's movements, oldest first.</summary>
    Task<IReadOnlyList<PositionMovement>> ListByPositionAsync(Guid positionId, CancellationToken ct = default);

    /// <summary>The position's latest movement, the only one that can be undone.</summary>
    Task<PositionMovement?> FindLatestAsync(Guid positionId, CancellationToken ct = default);

    /// <summary>The movements of several positions, in no particular order.</summary>
    Task<IReadOnlyList<PositionMovement>> ListByPositionsAsync(IReadOnlyCollection<Guid> positionIds, CancellationToken ct = default);

    Task<bool> AnyForPositionAsync(Guid positionId, CancellationToken ct = default);

    /// <summary>True when the ledger entry was created by a movement (and can only change through it).</summary>
    Task<bool> ExistsForTransactionAsync(Guid transactionId, CancellationToken ct = default);
}
