using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.Interceptors;

/// <summary>Captures every page writer in the same transaction as its change.</summary>
internal sealed class PageVersionSaveChangesInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    private void Capture(DbContext? context)
    {
        if (context is null)
            return;

        context.ChangeTracker.DetectChanges();
        foreach (var entry in context.ChangeTracker.Entries<Page>().ToList())
        {
            var isNew = entry.State == EntityState.Added;
            if (!isNew && entry.State != EntityState.Modified)
                continue;

            var changed = isNew || entry.Entity.RestoredFrom is not null ||
                PageSnapshot.From(entry.Entity) != PageSnapshot.From((Page)entry.OriginalValues.ToObject());
            if (!changed)
                continue;

            context.Set<PageVersion>().Add(entry.Entity.RecordVersion(timeProvider, isNew));
        }
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void ClearSources(DbContext? context)
    {
        if (context is null)
            return;

        foreach (var entry in context.ChangeTracker.Entries<Page>())
            entry.Entity.ClearHistorySource();
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        ClearSources(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        ClearSources(eventData.Context);
        return ValueTask.FromResult(result);
    }
}
