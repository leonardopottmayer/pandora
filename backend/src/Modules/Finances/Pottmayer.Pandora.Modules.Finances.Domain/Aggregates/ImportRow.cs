using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// One data line extracted from an <see cref="ImportFile"/>. Preserves the raw source text and
/// records every dedup decision so the user can audit the full pipeline.
/// </summary>
public sealed class ImportRow : AggregateRoot<Guid>
{
    public Guid ImportFileId { get; private set; }
    public int RowIndex { get; private set; }
    public string RawData { get; private set; } = string.Empty;
    public string? ParsedPayload { get; private set; }
    public string? ExternalId { get; private set; }
    public string? DedupKey { get; private set; }
    public DedupStatus DedupStatus { get; private set; } = DedupStatus.New;
    public Guid? MatchedTransactionId { get; private set; }
    public Guid? MatchedPendingTransactionId { get; private set; }
    public short? InstallmentNumber { get; private set; }
    public short? InstallmentCount { get; private set; }
    public Guid? MatchedInstallmentPlanId { get; private set; }
    public Guid? PendingTransactionId { get; private set; }
    public ImportRowStatus Status { get; private set; } = ImportRowStatus.Pending;
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private ImportRow() { }

    public static ImportRow CreatePending(
        Guid importFileId,
        int rowIndex,
        string rawData,
        DateTimeOffset now)
    {
        return new ImportRow
        {
            Id = Guid.CreateVersion7(),
            ImportFileId = importFileId,
            RowIndex = rowIndex,
            RawData = rawData,
            Status = ImportRowStatus.Pending,
            CreatedAt = now
        };
    }

    public void SetParsed(
        string parsedPayload,
        string? externalId,
        string? dedupKey,
        short? installmentNumber,
        short? installmentCount)
    {
        ParsedPayload = parsedPayload;
        ExternalId = externalId;
        DedupKey = dedupKey;
        InstallmentNumber = installmentNumber;
        InstallmentCount = installmentCount;
    }

    public void SetDedup(
        DedupStatus dedupStatus,
        Guid? matchedTransactionId,
        Guid? matchedPendingTransactionId)
    {
        DedupStatus = dedupStatus;
        MatchedTransactionId = matchedTransactionId;
        MatchedPendingTransactionId = matchedPendingTransactionId;
    }

    public void SetMatchedInstallmentPlan(Guid installmentPlanId)
        => MatchedInstallmentPlanId = installmentPlanId;

    /// <summary>
    /// Records a user-confirmed link to an existing transaction. Persisting the match here is what
    /// lets a future re-import of the same line resolve straight to that transaction (the dedup key
    /// stays unchanged, so <see cref="DuplicateDetector"/> finds this row and surfaces the link).
    /// </summary>
    public void MarkMatched(Guid transactionId)
    {
        MatchedTransactionId = transactionId;
        DedupStatus = DedupStatus.Matched;
    }

    public void MarkSuggestionCreated(Guid pendingTransactionId)
    {
        PendingTransactionId = pendingTransactionId;
        Status = ImportRowStatus.SuggestionCreated;
    }

    public void MarkSkipped()
        => Status = ImportRowStatus.Skipped;

    public void MarkError(string errorMessage)
    {
        ErrorMessage = errorMessage;
        Status = ImportRowStatus.Error;
    }
}
