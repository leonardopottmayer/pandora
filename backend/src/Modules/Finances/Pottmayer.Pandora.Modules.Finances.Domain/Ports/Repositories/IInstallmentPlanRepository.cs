using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

public interface IInstallmentPlanRepository : IStandardRepository<InstallmentPlan, Guid>
{
    Task<InstallmentPlan?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<InstallmentPlan>> GetByCardAsync(Guid cardId, Guid userId, CancellationToken ct = default);
}
