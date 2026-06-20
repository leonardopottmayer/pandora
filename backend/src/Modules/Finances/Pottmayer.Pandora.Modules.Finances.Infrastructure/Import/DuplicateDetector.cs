using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;

namespace Pottmayer.Pandora.Modules.Finances.Infrastructure.Import;

/// <summary>
/// Three-level dedup:
///   1. Certain  — same FITID (external_id) or same content hash already imported for this
///                 user+destination; generates a suggestion linked to the existing entity.
///   2. Suspected — different hash but ±2-day date + same amount against existing transactions;
///                  generates a flagged suggestion.
///   3. New      — no match; normal suggestion.
/// Repositories are passed in by the caller so this detector shares the active UoW.
/// </summary>
internal sealed class DuplicateDetector : IDuplicateDetector
{
    private static readonly int FuzzyDayWindow = 2;

    public async Task<IReadOnlyList<DedupResult>> DetectAsync(
        Guid userId, Guid? accountId, Guid? cardId,
        IReadOnlyList<ParsedImportRow> rows,
        IImportRowRepository importRowRepo,
        IImportFileRepository fileRepo,
        ITransactionRepository transactionRepo,
        IPendingTransactionRepository pendingRepo,
        CancellationToken ct = default)
    {
        var results = new DedupResult[rows.Count];

        // Pre-compute dedup keys for all rows
        var keys = rows.Select(r => ComputeDedupKey(r, accountId ?? cardId!.Value)).ToList();

        // Batch check existing import rows by dedup key
        var allKeys = keys.Where(k => k is not null).Distinct().ToList();
        var existingByKey = await BatchFindByKeyAsync(userId, accountId, cardId, allKeys!, importRowRepo, fileRepo, ct);

        // Batch check existing import rows by external_id
        var allExternalIds = rows
            .Where(r => r.ExternalId is not null)
            .Select(r => r.ExternalId!)
            .Distinct()
            .ToList();
        var existingByExtId = await BatchFindByExternalIdAsync(userId, accountId, cardId, allExternalIds, importRowRepo, fileRepo, ct);

        // For fuzzy matching: load recent transactions in the date range covered by these rows.
        // Clamp the window so a row with a missing/min date can't push the day number out of range.
        var (minDate, maxDate) = GetDateRange(rows);
        var fuzzyWindow = DateOnly.FromDayNumber(Math.Max(DateOnly.MinValue.DayNumber, minDate.DayNumber - FuzzyDayWindow));
        var fuzzyEnd = DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber, maxDate.DayNumber + FuzzyDayWindow));
        var recentTransactions = await LoadTransactionsInRangeAsync(
            userId, accountId, cardId, fuzzyWindow, fuzzyEnd, transactionRepo, ct);

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var key = keys[i];

            // 1 — Certain: exact external_id match
            if (row.ExternalId is not null && existingByExtId.TryGetValue(row.ExternalId, out var extMatches))
            {
                var (txId, pendingId) = ResolveLinks(extMatches);
                results[i] = new DedupResult(i, key ?? string.Empty, "certain", txId, pendingId);
                continue;
            }

            // 2 — Certain: dedup key match
            if (key is not null && existingByKey.TryGetValue(key, out var keyMatches))
            {
                var (txId, pendingId) = ResolveLinks(keyMatches);
                results[i] = new DedupResult(i, key, "certain", txId, pendingId);
                continue;
            }

            // 3 — Suspected: fuzzy transaction match
            var fuzzyMatch = FindFuzzyMatch(row, recentTransactions);
            if (fuzzyMatch is not null)
            {
                results[i] = new DedupResult(i, key ?? BuildFallbackKey(row, accountId ?? cardId!.Value),
                    "suspected", fuzzyMatch, null);
                continue;
            }

            results[i] = new DedupResult(i, key ?? BuildFallbackKey(row, accountId ?? cardId!.Value),
                "new", null, null);
        }

        return results;
    }

    // ─── Key computation ─────────────────────────────────────────────────────

    internal static string? ComputeDedupKey(ParsedImportRow row, Guid destId)
    {
        if (row.ExternalId is not null)
        {
            // FITID-based key
            return ComputeHash($"{destId}:fitid:{row.ExternalId}");
        }
        // Content-based key
        return BuildFallbackKey(row, destId);
    }

    internal static string BuildFallbackKey(ParsedImportRow row, Guid destId)
    {
        var normalizedDesc = NormalizeDescription(row.Description);
        var amountStr = row.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        return ComputeHash($"{destId}:hash:{row.OccurredOn:yyyy-MM-dd}:{amountStr}:{normalizedDesc}");
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    private static string NormalizeDescription(string desc)
        => Regex.Replace(desc.ToLowerInvariant().Trim(), @"\s+", " ");

    // ─── Batch repository queries ────────────────────────────────────────────

    private static async Task<Dictionary<string, List<ImportRow>>> BatchFindByKeyAsync(
        Guid userId, Guid? accountId, Guid? cardId,
        IReadOnlyList<string> keys, IImportRowRepository importRowRepo, IImportFileRepository fileRepo, CancellationToken ct)
    {
        if (keys.Count == 0) return [];
        var dict = new Dictionary<string, List<ImportRow>>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            var matches = await importRowRepo.FindByDedupKeyAsync(userId, accountId, cardId, key, fileRepo, ct);
            if (matches.Count > 0) dict[key] = [.. matches];
        }
        return dict;
    }

    private static async Task<Dictionary<string, List<ImportRow>>> BatchFindByExternalIdAsync(
        Guid userId, Guid? accountId, Guid? cardId,
        IReadOnlyList<string> externalIds, IImportRowRepository importRowRepo, IImportFileRepository fileRepo, CancellationToken ct)
    {
        if (externalIds.Count == 0) return [];
        var dict = new Dictionary<string, List<ImportRow>>(StringComparer.Ordinal);
        foreach (var extId in externalIds)
        {
            var matches = await importRowRepo.FindByExternalIdAsync(userId, accountId, cardId, extId, fileRepo, ct);
            if (matches.Count > 0) dict[extId] = [.. matches];
        }
        return dict;
    }

    private static async Task<IReadOnlyList<Transaction>> LoadTransactionsInRangeAsync(
        Guid userId, Guid? accountId, Guid? cardId,
        DateOnly from, DateOnly to, ITransactionRepository transactionRepo, CancellationToken ct)
    {
        var filter = new TransactionFilter
        {
            AccountId = accountId,
            CardId = cardId,
            From = from,
            To = to,
            Skip = 0,
            Take = 1000
        };
        return await transactionRepo.QueryAsync(userId, filter, ct);
    }

    // ─── Fuzzy match ─────────────────────────────────────────────────────────

    private static Guid? FindFuzzyMatch(ParsedImportRow row, IReadOnlyList<Transaction> candidates)
    {
        foreach (var tx in candidates)
        {
            var dateDiff = Math.Abs(tx.OccurredOn.DayNumber - row.OccurredOn.DayNumber);
            if (dateDiff > FuzzyDayWindow) continue;
            if (Math.Abs(tx.Amount - row.Amount) > 0.01m) continue;
            return tx.Id;
        }
        return null;
    }

    // ─── Link resolution ─────────────────────────────────────────────────────

    private static (Guid? TransactionId, Guid? PendingId) ResolveLinks(List<ImportRow> existingRows)
    {
        // A user-confirmed manual link wins: surface the matched transaction so the new suggestion
        // points straight at the real movement instead of an earlier (also-duplicate) suggestion.
        foreach (var existing in existingRows)
        {
            if (existing.MatchedTransactionId is not null)
                return (existing.MatchedTransactionId, null);
        }
        foreach (var existing in existingRows)
        {
            if (existing.PendingTransactionId is not null)
                return (null, existing.PendingTransactionId);
        }
        return (null, null);
    }

    // ─── Date range helper ───────────────────────────────────────────────────

    private static (DateOnly Min, DateOnly Max) GetDateRange(IReadOnlyList<ParsedImportRow> rows)
    {
        if (rows.Count == 0) return (DateOnly.MinValue, DateOnly.MaxValue);
        var min = rows[0].OccurredOn;
        var max = rows[0].OccurredOn;
        foreach (var r in rows)
        {
            if (r.OccurredOn < min) min = r.OccurredOn;
            if (r.OccurredOn > max) max = r.OccurredOn;
        }
        return (min, max);
    }
}
