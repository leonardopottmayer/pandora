using Pottmayer.Pandora.Modules.Notes.Abstractions;
using Pottmayer.Pandora.Shared.Domain.Storage;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Notes.Persistence.Storage;

/// <summary>
/// <see cref="IFileStorage"/> backed by a Postgres table. The blob's row id is the storage key. Each
/// operation runs in its own unit of work — the storage contract is deliberately independent of any
/// caller transaction, matching how an object store would behave.
/// </summary>
internal sealed class DatabaseFileStorage(IUnitOfWorkFactory factory, TimeProvider timeProvider) : IFileStorage
{
    public string Backend => FileStorageBackends.Database;

    public async Task<string> SaveAsync(
        string fileName, string contentType, byte[] content, CancellationToken ct = default)
    {
        var id = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var blob = FileBlob.Create(fileName, contentType, content, timeProvider.GetUtcNow());
            await ctx.AcquireRepository<IFileBlobRepository>().AddAsync(blob, token);
            return blob.Id;
        }, cancellationToken: ct);

        return id.ToString();
    }

    public async Task<StoredFile?> GetAsync(string storageKey, CancellationToken ct = default)
    {
        if (!Guid.TryParse(storageKey, out var id))
            return null;

        var blob = await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
            await ctx.AcquireRepository<IFileBlobRepository>().GetByIdAsync(id, token),
            cancellationToken: ct);

        return blob is null
            ? null
            : new StoredFile(blob.FileName, blob.ContentType, blob.SizeBytes, blob.Content);
    }

    public async Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        if (!Guid.TryParse(storageKey, out var id))
            return;

        await factory.ExecuteAsync(NotesModule.DatabaseKey, async (ctx, token) =>
        {
            var repo = ctx.AcquireRepository<IFileBlobRepository>();
            var blob = await repo.GetByIdAsync(id, token);
            if (blob is not null)
                await repo.RemoveAsync(blob, token);
            return true;
        }, cancellationToken: ct);
    }
}
