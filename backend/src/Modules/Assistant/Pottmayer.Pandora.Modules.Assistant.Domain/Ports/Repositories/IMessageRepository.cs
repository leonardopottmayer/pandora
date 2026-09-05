using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;

public interface IMessageRepository : IStandardRepository<Message, Guid>
{
    /// <summary>
    /// The conversation's most recent messages, oldest-first, capped at <paramref name="limit"/> — the
    /// multi-turn context re-sent to the model so a follow-up is understood.
    /// </summary>
    Task<IReadOnlyList<Message>> GetRecentByConversationAsync(Guid conversationId, int limit, CancellationToken ct = default);
}
