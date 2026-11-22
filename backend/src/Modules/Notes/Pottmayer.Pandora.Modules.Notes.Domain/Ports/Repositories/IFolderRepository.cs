using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;

public interface IFolderRepository : IStandardRepository<Folder, Guid>
{
    /// <summary>One non-deleted folder owned by the user, or <c>null</c> (404-on-foreign-resource rule).</summary>
    Task<Folder?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>The user's non-deleted folders, of one space or all, by parent then order and name.</summary>
    Task<IReadOnlyList<Folder>> GetForUserAsync(Guid userId, Guid? spaceId = null, CancellationToken ct = default);

    /// <summary>Whether the space holds any non-deleted folder.</summary>
    Task<bool> AnyInSpaceAsync(Guid spaceId, CancellationToken ct = default);
}
