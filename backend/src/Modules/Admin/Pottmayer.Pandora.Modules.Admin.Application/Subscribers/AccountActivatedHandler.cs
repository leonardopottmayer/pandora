using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Admin.Abstractions;
using Pottmayer.Pandora.Modules.Admin.Application.Bootstrap;
using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Admin.Application.Subscribers;

/// <summary>
/// Gives a newly activated account the default role, and the <c>admin</c> role when its e-mail is one of
/// the bootstrap e-mails. Both grants are idempotent, so a redelivered event changes nothing.
/// </summary>
public sealed class AccountActivatedHandler(RoleAssigner assigner, IOptions<AdminOptions> options)
    : IIntegrationEventHandler<AccountActivated>
{
    public async Task HandleAsync(AccountActivated @event, CancellationToken cancellationToken = default)
    {
        await assigner.AssignDefaultAsync(@event.UserId, cancellationToken);

        if (BootstrapEmails.Contains(options.Value, @event.Email))
            await assigner.AssignAdminAsync(@event.UserId, cancellationToken);
    }
}
