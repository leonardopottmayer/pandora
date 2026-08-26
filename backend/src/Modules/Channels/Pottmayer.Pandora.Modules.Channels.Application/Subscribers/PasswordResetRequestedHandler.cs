using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Application.Enqueue;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Application.Subscribers;

/// <summary>
/// Maps Identity's <see cref="PasswordResetRequested"/> to the <c>password-reset</c> template
/// and enqueues an e-mail. The producer knows nothing about templates or channels.
/// </summary>
public sealed class PasswordResetRequestedHandler(NotificationEnqueuer enqueuer, IOptions<ChannelsOptions> options)
    : IIntegrationEventHandler<PasswordResetRequested>
{
    private static readonly TemplateKey Template = TemplateKey.Create("password-reset");

    private string ResetUrl(string token) =>
        options.Value.PasswordResetUrlTemplate.Replace("{token}", Uri.EscapeDataString(token), StringComparison.Ordinal);

    public Task HandleAsync(PasswordResetRequested @event, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, string>
        {
            ["userId"] = @event.UserId.ToString(),
            ["email"] = @event.Email,
            ["token"] = @event.Token,
            ["resetUrl"] = ResetUrl(@event.Token)
        };

        return enqueuer.EnqueueAsync(
            Channel.Email,
            @event.Email,
            Template,
            Locale.Normalize(@event.Locale),
            payload,
            @event.EventId,
            userId: @event.UserId,
            category: "identity.security",
            ct: cancellationToken);
    }
}
