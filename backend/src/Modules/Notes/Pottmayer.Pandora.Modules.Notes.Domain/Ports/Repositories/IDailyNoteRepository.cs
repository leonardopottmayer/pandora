using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Tars.Data.Abstractions.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;

/// <summary>Resolves the page registered for a user's calendar day.</summary>
public interface IDailyNoteRepository : IStandardRepository<DailyNote, Guid>
{
    Task<DailyNote?> FindForDayAsync(Guid userId, DateOnly date, CancellationToken ct = default);
}
