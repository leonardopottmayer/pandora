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
        => Queryable().FirstOrDefaultAsync(
            s => s.CardId == cardId && s.UserId == userId && s.ReferenceMonth == referenceMonth, ct);

    public async Task<IReadOnlyList<CardStatement>> GetByCardAsync(
        Guid cardId, Guid userId, CancellationToken ct = default)
        => await Queryable()
            .Where(s => s.CardId == cardId && s.UserId == userId)
            .OrderByDescending(s => s.ReferenceMonth)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CardStatement>> GetLifecycleCandidatesAsync(
        DateOnly today, CancellationToken ct = default)
        => await Queryable()
            .Where(s =>
                (s.Status == StatementStatus.Open && s.ClosingDate <= today) ||
                (s.Status != StatementStatus.Paid && s.Status != StatementStatus.Overdue && s.DueDate < today))
            .ToListAsync(ct);
}
