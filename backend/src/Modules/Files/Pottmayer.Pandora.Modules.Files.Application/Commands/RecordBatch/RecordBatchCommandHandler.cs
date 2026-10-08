using Pottmayer.Pandora.Modules.Files.Abstractions;
using Pottmayer.Pandora.Modules.Files.Agent;
using Pottmayer.Pandora.Modules.Files.Application.Catalog;
using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.Errors;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;

namespace Pottmayer.Pandora.Modules.Files.Application.Commands.RecordBatch;

/// <summary>
/// Matches a batch against the catalog of the scan's root: an unchanged entry is only marked seen; a new
/// or changed file is stored and, without a fingerprint, returned so the agent sends it again with one.
/// </summary>
public sealed class RecordBatchCommandHandler(IUnitOfWorkFactory factory, TimeProvider timeProvider)
    : CommandHandlerBase<RecordBatchCommand, ScanBatchResult>
{
    protected override async Task<Result<ScanBatchResult>> HandleAsync(RecordBatchCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var items = input.Entries ?? [];
        if (items.Count > ScanBatch.MaxEntries) return Fail(FilesErrors.BatchTooLarge);

        // The agent is the user's own device, but its input is still checked: paths end up in the catalog as keys.
        var batch = new Dictionary<string, (ScannedEntry Item, EntryKind Kind)>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (!EntryKind.IsSupported(item.Kind)) return Fail(FilesErrors.InvalidEntryKind);
            if (!RootRules.TryNormalize(item.Path, out var path) || path == CatalogPath.Root)
                return Fail(FilesErrors.InvalidPath(item.Path));
            if (item.Fingerprint is not null && !Fingerprint.IsValid(item.Fingerprint)) return Fail(FilesErrors.InvalidFingerprint);
            batch[path] = (item, EntryKind.FromValue(item.Kind));
        }

        return await factory.ExecuteAsync<Result<ScanBatchResult>>(FilesModule.DatabaseKey, async (ctx, token) =>
        {
            var found = await AgentScans.FindRunningAsync(ctx, input.DeviceId, input.ScanId, token);
            if (found.IsFailure) return Result<ScanBatchResult>.Failure(found.Errors);
            var (scan, root) = found.Value;

            var now = timeProvider.GetUtcNow();
            var entries = ctx.AcquireRepository<IEntryRepository>();
            var existing = (await entries.ListByPathsAsync(root.Id, batch.Keys, token))
                .ToDictionary(e => e.RelativePath, StringComparer.Ordinal);

            int seen = 0, created = 0, changed = 0;
            var needsFingerprint = new List<string>();
            foreach (var (path, (item, kind)) in batch)
            {
                var size = Math.Max(0, item.Size);
                // ponytail: a folder replaced by a file of the same name (or the reverse) keeps its old kind
                // until the user forgets it; rare enough not to handle.
                if (existing.TryGetValue(path, out var entry))
                {
                    if (entry.LastSeenScanId != scan.Id) seen++;
                    if (entry.See(scan.Id, size, item.ModifiedAt, item.Fingerprint)) changed++;
                }
                else
                {
                    entry = Entry.Create(root.UserId, root.Id, scan.Id, kind, path, size, item.ModifiedAt, item.Fingerprint, now);
                    await entries.AddAsync(entry, token);
                    seen++;
                    created++;
                }

                if (entry.NeedsFingerprint) needsFingerprint.Add(path);
            }

            scan.RecordBatch(now, seen, created, changed);
            return Result<ScanBatchResult>.Success(new ScanBatchResult(needsFingerprint));
        }, cancellationToken: ct);
    }
}
