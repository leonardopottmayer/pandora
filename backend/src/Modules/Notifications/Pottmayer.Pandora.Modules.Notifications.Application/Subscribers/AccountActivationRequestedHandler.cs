using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Pandora.Modules.Notifications.Application.Enqueue;
using Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Notifications.Application.Subscribers;

/// <summary>
/// Maps Identity's <see cref="AccountActivationRequested"/> to the <c>account-activation</c> template
/// and enqueues an e-mail. The producer knows nothing about templates or channels.
/// </summary>
public sealed class AccountActivationRequestedHandler(NotificationEnqueuer enqueuer)
    : IIntegrationEventHandler<AccountActivationRequested>
{
    private static readonly TemplateKey Template = TemplateKey.Create("account-activation");

    public Task HandleAsync(AccountActivationRequested @event, CancellationToken cancellationToken = default)
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
