using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;

public interface IMailLabelRepository : IStandardRepository<MailLabel, Guid>
{
    Task<IReadOnlyList<MailLabel>> ListByMailboxAsync(Guid mailboxId, CancellationToken ct = default);
}
