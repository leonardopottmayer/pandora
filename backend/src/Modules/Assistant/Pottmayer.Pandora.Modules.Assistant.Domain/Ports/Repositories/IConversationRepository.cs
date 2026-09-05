using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;

public interface IConversationRepository : IStandardRepository<Conversation, Guid>
{
    /// <summary>The user's most recently active conversation, or null when they have none. The caller decides whether it has lapsed.</summary>
    Task<Conversation?> FindMostRecentByUserAsync(Guid userId, CancellationToken ct = default);
}
