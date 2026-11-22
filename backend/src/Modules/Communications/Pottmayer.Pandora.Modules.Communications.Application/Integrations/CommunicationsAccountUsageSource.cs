using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Models;
using Pottmayer.Pandora.Modules.Integrations.Abstractions.Ports;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Communications.Application.Integrations;

/// <summary>Each mailbox uses one account: "Email · me@example.com" on the connections screen.</summary>
public sealed class CommunicationsAccountUsageSource(IUnitOfWorkFactory factory) : IExternalAccountUsageSource
{
    public const string MailPurpose = "mail";

    public async Task<IReadOnlyList<ExternalAccountUsage>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var mailboxes = await factory.ExecuteAsync(CommunicationsModule.DatabaseKey, (context, token) =>
            context.AcquireRepository<IMailboxRepository>().ListByUserAsync(userId, token), cancellationToken: ct);

        return mailboxes
            .Select(m => new ExternalAccountUsage(m.ExternalAccountId, CommunicationsModule.Name, MailPurpose, m.Address))
            .ToList();
    }
}
