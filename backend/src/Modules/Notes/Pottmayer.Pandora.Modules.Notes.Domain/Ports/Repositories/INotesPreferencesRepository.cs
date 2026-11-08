using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;

/// <summary>Notes preferences keyed by user id.</summary>
public interface INotesPreferencesRepository : IStandardRepository<NotesPreferences, Guid>
{
    /// <summary>Serializes daily notes, settings and tree moves; hold until after the unit of work commits.</summary>
    Task<IAsyncDisposable> LockForUserAsync(Guid userId, CancellationToken ct = default);
}
