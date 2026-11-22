using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;

public interface ICommandInvocationRepository : IStandardRepository<CommandInvocation, Guid>
{
    /// <summary>The user's most recent invocations, newest first, for the audit trail.</summary>
    Task<IReadOnlyList<CommandInvocation>> ListRecentByUserAsync(Guid userId, int limit, CancellationToken ct = default);

    /// <summary>The calls of a conversation still waiting for a Confirm / Cancel at <paramref name="now"/>, oldest first.</summary>
    Task<IReadOnlyList<CommandInvocation>> ListAwaitingConfirmationAsync(Guid conversationId, DateTimeOffset now, CancellationToken ct = default);
}
