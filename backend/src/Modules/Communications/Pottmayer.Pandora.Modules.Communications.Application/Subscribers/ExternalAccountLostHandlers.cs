using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Integrations.Contracts;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Communications.Application.Subscribers;

/// <summary>The Google grant was revoked: its mailboxes stop until the user reconnects (their mail stays).</summary>
public sealed class ExternalAccountRevokedHandler(IUnitOfWorkFactory factory)
    : IIntegrationEventHandler<ExternalAccountRevoked>
{
    public Task HandleAsync(ExternalAccountRevoked @event, CancellationToken cancellationToken = default) =>
        MailboxesOnAccount.MarkNeedsReconnectAsync(factory, @event.ExternalAccountId, "The account's access was revoked.", cancellationToken);
}

/// <summary>The user disconnected the account: its mailboxes stop, their mail stays until the mailbox is removed.</summary>
public sealed class ExternalAccountDisconnectedHandler(IUnitOfWorkFactory factory)
    : IIntegrationEventHandler<ExternalAccountDisconnected>
{
    public Task HandleAsync(ExternalAccountDisconnected @event, CancellationToken cancellationToken = default) =>
        MailboxesOnAccount.MarkNeedsReconnectAsync(factory, @event.ExternalAccountId, "The account was disconnected.", cancellationToken);
}

internal static class MailboxesOnAccount
{
    public static Task MarkNeedsReconnectAsync(IUnitOfWorkFactory factory, Guid accountId, string reason, CancellationToken ct) =>
        factory.ExecuteAsync(CommunicationsModule.DatabaseKey, async (context, token) =>
        {
            var repository = context.AcquireRepository<IMailboxRepository>();
            foreach (var mailbox in await repository.ListByExternalAccountAsync(accountId, token))
            {
                mailbox.MarkNeedsReconnect(reason);
                await repository.UpdateAsync(mailbox, token);
            }
        }, cancellationToken: ct);
}
