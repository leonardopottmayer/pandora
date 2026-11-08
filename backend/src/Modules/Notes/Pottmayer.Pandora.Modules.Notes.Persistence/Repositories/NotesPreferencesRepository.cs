using Microsoft.EntityFrameworkCore;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Relational.Repositories;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.Repositories;

public sealed class NotesPreferencesRepository(IDataContextAccessor accessor)
    : StandardRepository<NotesPreferences, Guid>(accessor), INotesPreferencesRepository
{
    public async Task<IAsyncDisposable> LockForUserAsync(Guid userId, CancellationToken ct = default)
    {
        // Tars starts the write transaction at SaveChanges, so this session lock must span the
        // preceding reads too. Keep its connection open, then release explicitly after commit.
        await DbContext.Database.OpenConnectionAsync(ct);
        await DbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_lock(hashtextextended({userId.ToString()}, 16016))", ct);
        return new UserLock(DbContext, userId);
    }

    private sealed class UserLock(DbContext db, Guid userId) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_unlock(hashtextextended({userId.ToString()}, 16016))");
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }
}
