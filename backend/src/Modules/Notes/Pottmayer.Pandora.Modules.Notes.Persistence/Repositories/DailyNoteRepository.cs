using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.Repositories;

public sealed class DailyNoteRepository(IDataContextAccessor accessor)
    : StandardRepository<DailyNote, Guid>(accessor), IDailyNoteRepository
{
    public Task<DailyNote?> FindForDayAsync(Guid userId, DateOnly date, CancellationToken ct = default) =>
        Queryable().FirstOrDefaultAsync(note => note.UserId == userId && note.Date == date, ct);
}
