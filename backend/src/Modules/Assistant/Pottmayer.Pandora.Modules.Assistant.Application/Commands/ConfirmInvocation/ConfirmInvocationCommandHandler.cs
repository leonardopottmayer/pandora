using System.Text.Json;
using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Abstractions.Commands;
using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Errors;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Commands.ConfirmInvocation;

/// <summary>
/// Executes a tool call the pipeline held for confirmation: it re-runs the exact stored arguments through
/// the owning command handler and records the real outcome on the same invocation row. A confirmation
/// that has expired (or was already answered) is refused — never silently run late.
/// </summary>
public sealed class ConfirmInvocationCommandHandler(
    IUnitOfWorkFactory factory,
    IEnumerable<IAssistantTool> tools,
    TimeProvider timeProvider)
    : CommandHandlerBase<ConfirmInvocationCommand, InterpretResultDto>
{
    protected override async Task<Result<InterpretResultDto>> HandleAsync(ConfirmInvocationCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var now = timeProvider.GetUtcNow();

        var invocation = await factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, token) =>
        {
            var repo = context.AcquireRepository<ICommandInvocationRepository>();
            return await repo.GetByIdAsync(input.InvocationId, token);
        }, cancellationToken: ct);

        if (invocation is null || invocation.UserId != input.UserId)
            return Fail(AssistantErrors.InvocationNotFound);

        if (invocation.Status != InvocationStatus.PendingConfirmation)
            return Fail(AssistantErrors.NotPendingConfirmation);

        // Expired before the user answered — settle it as expired and refuse.
        if (!invocation.IsAwaitingConfirmation(now))
        {
            invocation.MarkExpired();
            await PersistAsync(invocation, ct);
            return Fail(AssistantErrors.ConfirmationExpired);
        }

        var tool = tools.FirstOrDefault(t => t.Descriptor.Name == invocation.CommandName);
        if (tool is null)
        {
            invocation.MarkFailed($"Unknown command '{invocation.CommandName}'.");
            await PersistAsync(invocation, ct);
            return Ok(ToDto(invocation));
        }

        try
        {
            using var document = JsonDocument.Parse(invocation.ArgumentsJson ?? "{}");
            var outcome = await tool.ExecuteAsync(input.UserId, document.RootElement, ct);
            if (outcome.Success)
                invocation.MarkExecuted(outcome.Message);
            else
                invocation.MarkFailed(outcome.Message);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or JsonException)
        {
            invocation.MarkFailed(ex.Message);
        }

        await PersistAsync(invocation, ct);
        return Ok(ToDto(invocation));
    }

    private Task PersistAsync(CommandInvocation invocation, CancellationToken ct) =>
        factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, token) =>
        {
            var repo = context.AcquireRepository<ICommandInvocationRepository>();
            await repo.UpdateAsync(invocation, token);
            return true;
        }, cancellationToken: ct);

    private static InterpretResultDto ToDto(CommandInvocation invocation) =>
        new(invocation.Id, invocation.ConversationId, invocation.Status.Value,
            invocation.CommandName, invocation.ArgumentsJson, invocation.Result ?? invocation.Error ?? string.Empty);
}
