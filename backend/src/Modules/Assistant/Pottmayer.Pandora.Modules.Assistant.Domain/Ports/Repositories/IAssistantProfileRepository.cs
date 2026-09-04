using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;

public interface IAssistantProfileRepository : IStandardRepository<AssistantProfile, Guid>
{
    /// <summary>The user's single assistant profile, or null if they never configured one.</summary>
    Task<AssistantProfile?> FindByUserAsync(Guid userId, CancellationToken ct = default);
}
