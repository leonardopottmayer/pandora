using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;

public interface IMailRuleRepository : IStandardRepository<MailRule, Guid>
{
    /// <summary>In position order.</summary>
    Task<IReadOnlyList<MailRule>> ListByMailboxAsync(Guid mailboxId, CancellationToken ct = default);

    /// <summary>Instead of <c>RemoveAsync</c>, which EF 10 cannot save for an entity with a JSON complex collection.</summary>
    Task DeleteByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}
