using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
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

    public async Task<IReadOnlyList<CommandInvocation>> ListAwaitingConfirmationAsync(
        Guid conversationId, DateTimeOffset now, CancellationToken ct = default) =>
        await Queryable()
            .Where(i => i.ConversationId == conversationId
                        && i.Status == InvocationStatus.PendingConfirmation
                        && i.ExpiresAt > now)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync(ct);
}
