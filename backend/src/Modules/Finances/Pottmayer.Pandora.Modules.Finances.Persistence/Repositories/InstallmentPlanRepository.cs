using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Persistence.Repositories;

public sealed class InstallmentPlanRepository(IDataContextAccessor accessor)
    : StandardRepository<InstallmentPlan, Guid>(accessor), IInstallmentPlanRepository
{
    public Task<InstallmentPlan?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => Queryable().FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);

    public async Task<IReadOnlyList<InstallmentPlan>> GetByCardAsync(Guid cardId, Guid userId, CancellationToken ct = default)
        => await Queryable()
            .Where(p => p.CardId == cardId && p.UserId == userId)
            .OrderByDescending(p => p.FirstReferenceMonth)
            .ThenByDescending(p => p.Id)
            .ToListAsync(ct);
}
