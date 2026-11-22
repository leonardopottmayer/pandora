using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Assistant.Persistence.Repositories;

public sealed class AssistantProfileRepository(IDataContextAccessor accessor)
    : StandardRepository<AssistantProfile, Guid>(accessor), IAssistantProfileRepository
{
    public Task<AssistantProfile?> FindByUserAsync(Guid userId, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(p => p.UserId == userId, ct);
}
