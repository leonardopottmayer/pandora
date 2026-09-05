using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Errors;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Commands.CancelInvocation;

/// <summary>Cancels a pending confirmation. Idempotent-ish: only a still-pending row can be declined.</summary>
public sealed class CancelInvocationCommandHandler(IUnitOfWorkFactory factory)
    : CommandHandlerBase<CancelInvocationCommand, InterpretResultDto>
{
    protected override async Task<Result<InterpretResultDto>> HandleAsync(CancelInvocationCommand request, CancellationToken ct)
    {
        var input = request.Input;

        var invocation = await factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, token) =>
        {
            var repo = context.AcquireRepository<ICommandInvocationRepository>();
            return await repo.GetByIdAsync(input.InvocationId, token);
        }, cancellationToken: ct);

        if (invocation is null || invocation.UserId != input.UserId)
            return Fail(AssistantErrors.InvocationNotFound);

        if (invocation.Status != InvocationStatus.PendingConfirmation)
            return Fail(AssistantErrors.NotPendingConfirmation);

        invocation.Cancel();

        await factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, token) =>
        {
            var repo = context.AcquireRepository<ICommandInvocationRepository>();
            await repo.UpdateAsync(invocation, token);
            return true;
        }, cancellationToken: ct);

        return Ok(new InterpretResultDto(
            invocation.Id, invocation.ConversationId, invocation.Status.Value,
            invocation.CommandName, invocation.ArgumentsJson, "Cancelled."));
    }
}
