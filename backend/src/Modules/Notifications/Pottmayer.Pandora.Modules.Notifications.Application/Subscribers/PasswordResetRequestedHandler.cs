using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Pandora.Modules.Notifications.Application.Enqueue;
using Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Notifications.Application.Subscribers;

/// <summary>
/// Maps Identity's <see cref="PasswordResetRequested"/> to the <c>password-reset</c> template
/// and enqueues an e-mail. The producer knows nothing about templates or channels.
/// </summary>
public sealed class PasswordResetRequestedHandler(NotificationEnqueuer enqueuer)
    : IIntegrationEventHandler<PasswordResetRequested>
{
    private static readonly TemplateKey Template = TemplateKey.Create("password-reset");

    public Task HandleAsync(PasswordResetRequested @event, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, string>
        {
            ["userId"] = @event.UserId.ToString(),
            ["email"] = @event.Email,
            ["token"] = @event.Token
        };

        return enqueuer.EnqueueAsync(
            Channel.Email,
            @event.Email,
            Template,
            Locale.Normalize(@event.Locale),
            payload,
            @event.EventId,
            cancellationToken);
    }
}
