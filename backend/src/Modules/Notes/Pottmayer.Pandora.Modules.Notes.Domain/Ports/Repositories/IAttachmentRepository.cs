using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;

public interface IAttachmentRepository : IStandardRepository<Attachment, Guid>
{
    /// <summary>One attachment the user uploaded, or <c>null</c> (used for the 404-on-foreign-resource rule).</summary>
    Task<Attachment?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);
}
