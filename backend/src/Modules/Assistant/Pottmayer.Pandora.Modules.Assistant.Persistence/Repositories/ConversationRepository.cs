using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Assistant.Persistence.Repositories;

public sealed class ConversationRepository(IDataContextAccessor accessor)
    : StandardRepository<Conversation, Guid>(accessor), IConversationRepository
{
    public Task<Conversation?> FindMostRecentByUserAsync(Guid userId, CancellationToken ct = default) =>
        Queryable()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.LastActivityAt)
            .FirstOrDefaultAsync(ct);
}
