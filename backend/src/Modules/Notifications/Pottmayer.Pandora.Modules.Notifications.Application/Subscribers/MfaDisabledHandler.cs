using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Pandora.Modules.Notifications.Application.Enqueue;
using Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Notifications.Application.Subscribers;

/// <summary>
/// Maps Identity's <see cref="MfaDisabled"/> to the <c>mfa-disabled</c> template and enqueues a
/// security notice (no token).
/// </summary>
public sealed class MfaDisabledHandler(NotificationEnqueuer enqueuer)
    : IIntegrationEventHandler<MfaDisabled>
{
    private static readonly TemplateKey Template = TemplateKey.Create("mfa-disabled");

    public Task HandleAsync(MfaDisabled @event, CancellationToken cancellationToken = default)
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
