using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Application.Commands.CancelInvocation;
using Pottmayer.Pandora.Modules.Assistant.Application.Commands.ConfirmInvocation;
using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Pandora.Modules.Assistant.Application.Interpret;
using Pottmayer.Pandora.Modules.Assistant.Domain.Errors;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Assistant.Application.Subscribers;

/// <summary>
/// Turns a tap on a Confirm / Cancel button (declared by <see cref="InboundMessageReceivedHandler"/>) into
/// the same confirm/cancel commands the web uses, and replies on the assistant bot with the outcome. It
/// answers only for its own buttons (<c>OwnerModule == "assistant"</c>); the payload is the invocation id.
/// </summary>
public sealed class AssistantInteractionReceivedHandler(
    IUnitOfWorkFactory factory,
    ISender sender,
    IIntegrationEventBus bus,
    AssistantToolContextResolver contexts,
    TimeProvider timeProvider)
    : IIntegrationEventHandler<InboundInteractionReceived>
{
    public const string ConfirmAction = "confirm";
    public const string CancelAction = "cancel";

    public async Task HandleAsync(InboundInteractionReceived @event, CancellationToken cancellationToken = default)
    {
        if (@event.OwnerModule != AssistantModule.Name || !Guid.TryParse(@event.Payload, out var invocationId))
            return;

        Result<InvocationResultDto> result;
        switch (@event.Action)
        {
            case ConfirmAction:
                result = await sender.Send(
                    new ConfirmInvocationCommand(new ConfirmInvocationInput(@event.UserId, invocationId)), cancellationToken);
                break;
            case CancelAction:
                result = await sender.Send(
                    new CancelInvocationCommand(new CancelInvocationInput(@event.UserId, invocationId)), cancellationToken);
                break;
            default:
                return;
        }

        var reply = result.IsSuccess ? result.Value!.Message : await RefusalAsync(@event.UserId, result.Errors, cancellationToken);

        await factory.ExecuteAsync(AssistantModule.DatabaseKey, async (context, token) =>
        {
            await bus.PublishAsync(
                new SendAssistantReply(Guid.CreateVersion7(), timeProvider.GetUtcNow(), @event.UserId, AssistantModule.Name, reply),
                token);
            return true;
        }, cancellationToken: cancellationToken);
    }

    /// <summary>Why the tap did nothing, in the user's language: expired, or already answered.</summary>
    private async Task<string> RefusalAsync(Guid userId, IEnumerable<Error> errors, CancellationToken ct)
    {
        var context = await contexts.ResolveAsync(userId, ct);
        return errors.Any(e => e.Code == AssistantErrors.ConfirmationExpired.Code)
            ? context.Text("Essa confirmação expirou. Peça de novo.", "This confirmation has expired. Ask again.")
            : context.Text("Esse pedido já foi respondido.", "That request was already answered.");
    }
}
