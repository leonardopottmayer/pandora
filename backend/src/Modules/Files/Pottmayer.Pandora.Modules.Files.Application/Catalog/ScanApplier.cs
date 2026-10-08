using Pottmayer.Pandora.Modules.Files.Domain.Entities;
using Pottmayer.Pandora.Modules.Files.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Files.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;

namespace Pottmayer.Pandora.Modules.Files.Application.Catalog;

/// <summary>
/// Applies a finished scan to the catalog (product-plan §4.4), inside the caller's unit of work:
/// <list type="number">
/// <item>Moves — an unseen file whose fingerprint matches exactly one newly seen file keeps its id at the new path.</item>
/// <item>Each remaining unseen entry becomes <c>excluded</c> if the current rules leave it out, else <c>missing</c>.</item>
/// <item>Safety brake — when more than 20 % of the root would go missing, the scan is held for the user instead.</item>
/// </list>
/// </summary>
internal static class ScanApplier
{
    public static async Task ApplyAsync(
        IDataContext ctx, Root root, Scan scan, bool enforceBrake, DateTimeOffset now, CancellationToken ct)
    {
        var entries = ctx.AcquireRepository<IEntryRepository>();
        var presentBefore = await entries.CountPresentAsync(root.Id, scan.StartedAt, ct);

        var moves = await entries.FindMovesAsync(
            root.UserId, root.Id, scan.Id, root.LastCompletedScanAt ?? DateTimeOffset.UnixEpoch, ct);
        await entries.ApplyMovesAsync(moves, ct);
        var moved = moves.Select(m => m.OldId).ToHashSet();

        var rules = await RootRules.LoadAsync(ctx, root, ct);
        var missing = new List<Guid>();
        var excluded = new List<Guid>();
        foreach (var entry in await entries.ListUnseenAsync(root.Id, scan.Id, ct))
        {
            if (moved.Contains(entry.Id)) continue;
            (rules.Includes(entry.RelativePath, entry.Kind.Value) ? missing : excluded).Add(entry.Id);
        }

        // Exclusions come from the user's own config change, so only missing entries count towards the brake.
        if (enforceBrake && Scan.TripsSafetyBrake(missing.Count, presentBefore))
        {
            scan.Hold(now, moves.Count, missing.Count, excluded.Count);
            return;
        }

        await entries.MarkAsync(missing, EntryStatus.Missing, now, ct);
        await entries.MarkAsync(excluded, EntryStatus.Excluded, now, ct);
        scan.Complete(now, moves.Count, missing.Count, excluded.Count);
        root.RecordCompletedScan(now, await entries.CountPresentAsync(root.Id, ct: ct));
    }
}
