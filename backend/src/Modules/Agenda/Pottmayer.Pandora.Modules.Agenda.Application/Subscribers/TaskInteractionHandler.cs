using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CompleteTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Tasks;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Subscribers;

/// <summary>
/// Turns a task alert's inline-button tap into a task action. It answers only for its own buttons
/// (<c>OwnerModule == "agenda"</c>) whose payload is a task payload (<c>task|{guid}</c>) — a reminder
/// button, whose payload is a bare guid or <c>guid|ticks</c>, is ignored here and handled by the
/// reminder handler. <c>task_done</c> completes the task, which for a recurring task also spawns its
/// next instance. Acting on an already-done task is a harmless no-op.
/// </summary>
public sealed class TaskInteractionHandler(ISender sender)
    : IIntegrationEventHandler<InboundInteractionReceived>
{
    private const string OwnerModule = "agenda";

    public async Task HandleAsync(InboundInteractionReceived @event, CancellationToken cancellationToken = default)
    {
        if (@event.OwnerModule != OwnerModule)
            return;

        if (!TaskButtonPayload.TryParse(@event.Payload, out var taskId))
            return;

        if (@event.Action == "task_done")
            await sender.Send(new CompleteTaskCommand(new CompleteTaskInput(@event.UserId, taskId)), cancellationToken);
    }
}
