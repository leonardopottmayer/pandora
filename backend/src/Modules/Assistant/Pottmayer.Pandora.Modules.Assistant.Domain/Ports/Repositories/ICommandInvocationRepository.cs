using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;

public interface ICommandInvocationRepository : IStandardRepository<CommandInvocation, Guid>
{
    /// <summary>The user's most recent invocations, newest first, for the audit trail.</summary>
    Task<IReadOnlyList<CommandInvocation>> ListRecentByUserAsync(Guid userId, int limit, CancellationToken ct = default);
}
