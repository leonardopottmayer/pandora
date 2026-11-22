using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Application.Enqueue;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Application.Subscribers;

/// <summary>
/// Maps Identity's <see cref="AccountActivationRequested"/> to the <c>account-activation</c> template
/// (or <c>account-activation-approval</c>, which says an administrator still has to approve the
/// account) and enqueues an e-mail. The producer knows nothing about templates or channels.
/// </summary>
public sealed class AccountActivationRequestedHandler(NotificationEnqueuer enqueuer, IOptions<ChannelsOptions> options)
    : IIntegrationEventHandler<AccountActivationRequested>
{
    private static readonly TemplateKey Template = TemplateKey.Create("account-activation");
    private static readonly TemplateKey WithApprovalTemplate = TemplateKey.Create("account-activation-approval");

    private string ActivationUrl(string token) =>
        options.Value.ActivationUrlTemplate.Replace("{token}", Uri.EscapeDataString(token), StringComparison.Ordinal);

    public Task HandleAsync(AccountActivationRequested @event, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, string>
        {
            ["userId"] = @event.UserId.ToString(),
            ["email"] = @event.Email,
            ["token"] = @event.Token,
            ["activationUrl"] = ActivationUrl(@event.Token)
        };

        return enqueuer.EnqueueAsync(
            Channel.Email,
            @event.Email,
            @event.RequiresApproval ? WithApprovalTemplate : Template,
            Locale.Normalize(@event.Locale),
            payload,
            @event.EventId,
            userId: @event.UserId,
            category: "identity.account",
            ct: cancellationToken);
    }
}
