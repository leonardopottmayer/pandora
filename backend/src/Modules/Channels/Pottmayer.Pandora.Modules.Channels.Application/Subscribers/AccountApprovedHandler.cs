using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Pandora.Modules.Channels.Application.Enqueue;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Channels.Application.Subscribers;

/// <summary>
/// Maps Identity's <see cref="AccountApproved"/> to the <c>account-approved</c> template and e-mails the
/// user that they can sign in.
/// </summary>
public sealed class AccountApprovedHandler(NotificationEnqueuer enqueuer)
    : IIntegrationEventHandler<AccountApproved>
{
    private static readonly TemplateKey Template = TemplateKey.Create("account-approved");

    public Task HandleAsync(AccountApproved @event, CancellationToken cancellationToken = default) =>
        enqueuer.EnqueueAsync(
            Channel.Email,
            @event.Email,
            Template,
            Locale.Normalize(@event.Locale),
            new Dictionary<string, string> { ["userId"] = @event.UserId.ToString(), ["email"] = @event.Email },
            @event.EventId,
            userId: @event.UserId,
            category: "identity.account",
            ct: cancellationToken);
}
