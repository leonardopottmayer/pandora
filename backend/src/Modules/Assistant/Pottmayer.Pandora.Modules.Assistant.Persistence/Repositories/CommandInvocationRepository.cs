using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Assistant.Persistence.Repositories;

public sealed class CommandInvocationRepository(IDataContextAccessor accessor)
    : StandardRepository<CommandInvocation, Guid>(accessor), ICommandInvocationRepository
{
    public async Task<IReadOnlyList<CommandInvocation>> ListRecentByUserAsync(
        Guid userId, int limit, CancellationToken ct = default) =>
        await Queryable()
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
}
