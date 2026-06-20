using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class CardStatementRepository(IDataContextAccessor accessor)
    : StandardRepository<CardStatement, Guid>(accessor), ICardStatementRepository
{
    public Task<CardStatement?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);

    public Task<CardStatement?> FindByCardAndReferenceMonthAsync(
        Guid cardId, Guid userId, string referenceMonth, CancellationToken ct = default)
    {
        // Check locally tracked (e.g. just-added, not-yet-committed) entities first: within a single
        // unit of work, multiple occurrences can resolve to the same reference month, and a DB query
        // alone won't see statements created earlier in the same not-yet-saved batch.
        var local = Set.Local.FirstOrDefault(
            s => s.CardId == cardId && s.UserId == userId && s.ReferenceMonth == referenceMonth);
        if (local is not null)
            return Task.FromResult<CardStatement?>(local);

        return Queryable().FirstOrDefaultAsync(
            s => s.CardId == cardId && s.UserId == userId && s.ReferenceMonth == referenceMonth, ct);
    }

    public async Task<IReadOnlyList<CardStatement>> GetByCardAsync(
        Guid cardId, Guid userId, CancellationToken ct = default)
        => await Queryable()
            .Where(s => s.CardId == cardId && s.UserId == userId)
            .OrderByDescending(s => s.ReferenceMonth)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CardStatement>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids, Guid userId, CancellationToken ct = default)
        => await Queryable()
            .Where(s => ids.Contains(s.Id) && s.UserId == userId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CardStatement>> GetLifecycleCandidatesAsync(
        DateOnly today, CancellationToken ct = default)
        => await Queryable()
            .Where(s =>
                (s.Status == StatementStatus.Open && s.ClosingDate <= today) ||
                (s.Status != StatementStatus.Paid && s.Status != StatementStatus.Overdue && s.DueDate < today))
            .ToListAsync(ct);
}
