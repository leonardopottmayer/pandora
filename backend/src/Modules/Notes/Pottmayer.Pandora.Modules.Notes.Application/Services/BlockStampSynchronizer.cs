using Pottmayer.Pandora.Modules.Notes.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notes.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notes.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Notes.Application.Services;

/// <summary>
/// Keeps <see cref="BlockStamp"/> in step with a page's blocks on every save: a new block gets a
/// stamp, a changed one is touched with what wrote it (<see cref="Page.HistorySource"/>), a removed
/// one loses its stamp. Saving the same blocks twice changes nothing.
/// </summary>
internal static class BlockStampSynchronizer
{
    public static async Task RebuildAsync(Page page, PageContent content, IBlockStampRepository stamps, TimeProvider timeProvider, CancellationToken ct)
    {
        var fingerprints = content.BlockFingerprints();
        var existing = await stamps.GetByPageAsync(page.Id, ct);

        var gone = existing.Where(s => !fingerprints.ContainsKey(s.BlockId)).ToList();
        if (gone.Count > 0)
            await stamps.RemoveRangeAsync(gone, ct);

        var byBlock = existing.ToDictionary(s => s.BlockId);
        foreach (var (blockId, fingerprint) in fingerprints)
        {
            if (!byBlock.TryGetValue(blockId, out var stamp))
                await stamps.AddAsync(BlockStamp.Create(page.Id, blockId, fingerprint, page.HistorySource, timeProvider), ct);
            else if (stamp.Touch(fingerprint, page.HistorySource, timeProvider))
                await stamps.UpdateAsync(stamp, ct);
        }
    }
}
