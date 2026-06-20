using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Ports.Services;

/// <summary>
/// Runs dedup checks for a batch of parsed rows against existing import rows and transactions,
/// returning one <see cref="DedupResult"/> per input row.
/// Repositories are passed in so callers can share the active unit-of-work context.
/// </summary>
public interface IDuplicateDetector
{
    Task<IReadOnlyList<DedupResult>> DetectAsync(
        Guid userId,
        Guid? accountId,
        Guid? cardId,
        IReadOnlyList<ParsedImportRow> rows,
        IImportRowRepository importRowRepo,
        IImportFileRepository fileRepo,
        ITransactionRepository transactionRepo,
        IPendingTransactionRepository pendingRepo,
        CancellationToken ct = default);
}

public sealed record DedupResult(
    int RowIndex,
    string DedupKey,
    /// <summary>new / certain / suspected / matched</summary>
    string DedupStatus,
    Guid? MatchedTransactionId,
    Guid? MatchedPendingTransactionId);
