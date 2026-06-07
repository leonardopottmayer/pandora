using Pottmayer.Pandora.Modules.Identity.Contracts;
using Pottmayer.Pandora.Modules.Notifications.Application.Enqueue;
using Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Notifications.Application.Subscribers;

/// <summary>
/// Maps Identity's <see cref="MfaEnabled"/> to the <c>mfa-enabled</c> template and enqueues an
/// informational e-mail (no token).
/// </summary>
public sealed class MfaEnabledHandler(NotificationEnqueuer enqueuer)
    : IIntegrationEventHandler<MfaEnabled>
{
    private static readonly TemplateKey Template = TemplateKey.Create("mfa-enabled");

    public Task HandleAsync(MfaEnabled @event, CancellationToken cancellationToken = default)
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
