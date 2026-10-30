using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;

/// <summary>Keyed by the message id.</summary>
public interface IMailContentRepository : IStandardRepository<MailContent, Guid>
{
    /// <summary>Every cached body not opened since <paramref name="cutoff"/> (index on <c>last_accessed_at</c>).</summary>
    Task<int> DeleteNotAccessedSinceAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}
