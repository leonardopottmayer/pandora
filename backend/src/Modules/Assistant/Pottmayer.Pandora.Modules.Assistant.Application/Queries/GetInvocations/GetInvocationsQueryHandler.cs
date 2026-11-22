using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Queries.GetInvocations;

/// <summary>
/// The user's recent invocations, newest first — the audit trail behind the command bar, so the exact
/// tool call the model produced is inspectable. A held call past its deadline reads as expired: the row
/// only turns <c>expired</c> when someone tries to confirm it, and no sweep runs before that.
/// </summary>
public sealed class GetInvocationsQueryHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : QueryHandlerBase<GetInvocationsQuery, IReadOnlyList<InvocationDto>>
{
    private const int MaxLimit = 100;

    protected override async Task<Result<IReadOnlyList<InvocationDto>>> HandleAsync(
        GetInvocationsQuery request, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Input.Limit, 1, MaxLimit);

        var invocations = await factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, ct) =>
        {
            var repo = context.AcquireRepository<ICommandInvocationRepository>();
            return await repo.ListRecentByUserAsync(request.Input.UserId, limit, ct);
        }, cancellationToken: cancellationToken);

        var now = timeProvider.GetUtcNow();
        var dtos = invocations
            .Select(i => new InvocationDto(
                i.Id, i.ConversationId, i.Utterance, i.CommandName, i.ArgumentsJson,
                i.Status == InvocationStatus.PendingConfirmation && !i.IsAwaitingConfirmation(now)
                    ? InvocationStatus.Expired.Value
                    : i.Status.Value,
                i.Result, i.Error, i.Provider, i.Model, i.LatencyMs, i.PromptTokens, i.CompletionTokens,
                i.ExpiresAt, i.CreatedAt))
            .ToList();

        return Ok((IReadOnlyList<InvocationDto>)dtos);
    }
}
