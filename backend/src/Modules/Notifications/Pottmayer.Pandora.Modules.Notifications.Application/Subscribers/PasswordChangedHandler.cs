using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Pandora.Modules.Notifications.Application.Enqueue;
using Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Notifications.Application.Subscribers;

/// <summary>
/// Maps Identity's <see cref="PasswordChanged"/> to the <c>password-changed</c> template
/// and enqueues an informational e-mail (no token).
/// </summary>
public sealed class PasswordChangedHandler(NotificationEnqueuer enqueuer)
    : IIntegrationEventHandler<PasswordChanged>
{
    private static readonly TemplateKey Template = TemplateKey.Create("password-changed");

    public Task HandleAsync(PasswordChanged @event, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, string>
        {
            ["userId"] = @event.UserId.ToString(),
            ["email"] = @event.Email
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
