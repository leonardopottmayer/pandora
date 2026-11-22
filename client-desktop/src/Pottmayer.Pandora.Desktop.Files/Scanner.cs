using Pottmayer.Pandora.Modules.Files.Agent;

namespace Pottmayer.Pandora.Desktop.Files;

/// <summary>Where a scan stands, as pushed to the page (<c>files.scanProgress</c>).</summary>
/// <param name="State"><c>running</c>, then <c>completed</c>, <c>held</c>, <c>aborted</c> or <c>failed</c>.</param>
internal sealed record ScanProgress(
    Guid RootId, Guid? ScanId, string State, int Walked, int Fingerprinted, string? Error = null, ScanOutcome? Outcome = null);

/// <summary>
/// Runs one scan of one root: walks it, sends batches of up to 1000 entries, fingerprints and reads the
/// metadata of the files the backend asks about, and completes. Any failure aborts the scan on the server, so nothing is marked
/// missing from a half walk.
/// </summary>
internal sealed class Scanner(FilesApi api)
{
    public async Task<ScanOutcome> RunAsync(RootConfig root, Action<ScanProgress> report, CancellationToken ct)
    {
        var scanId = await api.StartScanAsync(root.Id, ct);
        int walked = 0, fingerprinted = 0;
        report(new ScanProgress(root.Id, scanId, "running", 0, 0));

        try
        {
            if (!Directory.Exists(root.LocalPath))
                return await api.AbortAsync(scanId, "root-unavailable", ct);

            var rules = new CatalogRules(root.CaseSensitive, root.Marks, root.Filters);
            var batch = new List<WalkItem>(ScanBatch.MaxEntries);
            foreach (var item in Walker.Walk(root.LocalPath, rules, root.IncludeHidden, ct))
            {
                batch.Add(item);
                walked++;
                if (batch.Count < ScanBatch.MaxEntries) continue;

                fingerprinted += await SendAsync(scanId, batch, ct);
                batch.Clear();
                report(new ScanProgress(root.Id, scanId, "running", walked, fingerprinted));
            }
            if (batch.Count > 0) fingerprinted += await SendAsync(scanId, batch, ct);
            report(new ScanProgress(root.Id, scanId, "running", walked, fingerprinted));

            return await api.CompleteAsync(scanId, walked, ct);
        }
        catch (Exception ex)
        {
            await TryAbortAsync(scanId, ex is OperationCanceledException ? "cancelled" : "agent-error");
            throw;
        }
    }

    /// <summary>
    /// Sends a batch, then the fingerprints and metadata the backend asked for. Returns how many
    /// fingerprints were computed.
    /// </summary>
    private async Task<int> SendAsync(Guid scanId, List<WalkItem> batch, CancellationToken ct)
    {
        var result = await api.SendBatchAsync(scanId, [.. batch.Select(i => i.ToEntry(null))], ct);
        var needsFingerprint = result.NeedsFingerprint.ToHashSet(StringComparer.Ordinal);
        var needsMetadata = result.NeedsMetadata.ToHashSet(StringComparer.Ordinal);
        if (needsFingerprint.Count == 0 && needsMetadata.Count == 0) return 0;

        var byPath = batch.ToDictionary(i => i.Path, StringComparer.Ordinal);
        var answers = new List<ScannedEntry>();
        var fingerprinted = 0;
        foreach (var path in needsFingerprint.Union(needsMetadata))
        {
            if (!byPath.TryGetValue(path, out var item)) continue;

            // A file that cannot be read now (locked, gone) is asked about again on the next scan.
            var fingerprint = needsFingerprint.Contains(path) ? await TryFingerprintAsync(item.FullPath, ct) : null;
            var metadata = needsMetadata.Contains(path) ? MetadataReader.Read(item.FullPath) : null;
            if (fingerprint is null && metadata is null) continue;

            answers.Add(item.ToEntry(fingerprint, metadata));
            if (fingerprint is not null) fingerprinted++;
        }

        if (answers.Count > 0) await api.SendBatchAsync(scanId, answers, ct);
        return fingerprinted;
    }

    private static async Task<string?> TryFingerprintAsync(string fullPath, CancellationToken ct)
    {
        try
        {
            // Read-only, and sharing everything: the scan must never get in the way of whoever uses the file.
            await using var stream = new FileStream(
                fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
            return await Fingerprint.ComputeAsync(stream, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async Task TryAbortAsync(Guid scanId, string reason)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await api.AbortAsync(scanId, reason, timeout.Token);
        }
        catch (Exception)
        {
            // The backend expires a silent scan by itself; the abort only makes it faster.
        }
    }
}
