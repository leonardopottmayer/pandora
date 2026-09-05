using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Assistant.Persistence.Repositories;

public sealed class MessageRepository(IDataContextAccessor accessor)
    : StandardRepository<Message, Guid>(accessor), IMessageRepository
{
    public async Task<IReadOnlyList<Message>> GetRecentByConversationAsync(
        Guid conversationId, int limit, CancellationToken ct = default)
    {
        if (limit <= 0)
            return [];

        // Take the newest `limit` (indexed by created_at desc), then flip to chronological for the prompt.
        var recent = await Queryable()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Take(limit)
            .ToListAsync(ct);

        recent.Reverse();
        return recent;
    }
}
