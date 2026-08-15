using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Tars.Data.Relational.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;

public interface ITagRepository : IStandardRepository<Tag, Guid>
{
    /// <summary>One tag owned by the user, or <c>null</c> (404-on-foreign-resource rule).</summary>
    Task<Tag?> FindByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>Every tag of the user, ordered by name — the filter lists and the manage screen.</summary>
    Task<IReadOnlyList<Tag>> GetForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// The user's tags among <paramref name="slugs"/>, in one round trip: what a save needs to tell
    /// the tags it must create apart from the ones that already exist.
    /// </summary>
    Task<IReadOnlyList<Tag>> FindBySlugsAsync(
        Guid userId, IReadOnlyCollection<string> slugs, CancellationToken ct = default);

    /// <summary>The user's tags among <paramref name="ids"/> — validating a filter came from real tags.</summary>
    Task<IReadOnlyList<Tag>> GetByIdsForUserAsync(
        IReadOnlyCollection<Guid> ids, Guid userId, CancellationToken ct = default);
}
