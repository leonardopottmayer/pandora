using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// Tracks a single file uploaded for import. The file bytes are retained so the parsing job can
/// re-read them on retry. Status follows the state machine:
///   received → parsing → completed
///                      → failed   (manual retry available)
///                      → aborted  (terminal)
/// </summary>
public sealed class ImportFile : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public Guid? LayoutId { get; private set; }
    public Guid? AccountId { get; private set; }
    public Guid? CardId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string FileHash { get; private set; } = string.Empty;
    public byte[] FileContent { get; private set; } = [];
    public int FileSize { get; private set; }
    public Guid CorrelationId { get; private set; }

    /// <summary>
    /// Optional onboarding cutoff: rows dated before this are skipped during parsing (no suggestion).
    /// <c>null</c> imports everything. The cutoff day itself is kept (rows on/after it are imported).
    /// </summary>
    public DateOnly? CutoffDate { get; private set; }
    public ImportFileStatus Status { get; private set; } = ImportFileStatus.Received;
    public int TotalRows { get; private set; }
    public int ParsedRows { get; private set; }
    public int ErrorRows { get; private set; }
    public int DuplicateRows { get; private set; }
    public int SuggestionRows { get; private set; }
    public int RetryCount { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsReceived => Status == ImportFileStatus.Received;
    public bool IsParsing => Status == ImportFileStatus.Parsing;
    public bool IsTerminal => Status == ImportFileStatus.Completed || Status == ImportFileStatus.Aborted;

    private ImportFile() { }

    /// <summary>Registers a newly uploaded file as received, awaiting parsing.</summary>
    public static ImportFile Create(
        Guid userId,
        Guid? layoutId,
        Guid? accountId,
        Guid? cardId,
        string fileName,
        string fileHash,
        byte[] fileContent,
        DateOnly? cutoffDate,
        TimeProvider timeProvider)
    {
        return new ImportFile
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            LayoutId = layoutId,
            AccountId = accountId,
            CardId = cardId,
            FileName = fileName,
            FileHash = fileHash,
            FileContent = fileContent,
            FileSize = fileContent.Length,
            CorrelationId = Guid.CreateVersion7(),
            CutoffDate = cutoffDate,
            Status = ImportFileStatus.Received,
            CreatedAt = timeProvider.GetUtcNow()
        };
    }

    /// <summary>Begins parsing the file. No-op unless the file is still in the received state.</summary>
    public bool StartParsing(TimeProvider timeProvider)
    {
        if (!IsReceived) return false;
        Status = ImportFileStatus.Parsing;
        StartedAt = timeProvider.GetUtcNow();
        return true;
    }

    /// <summary>Records the outcome of a finished parse run and reaches the terminal completed state.</summary>
    public void Complete(int total, int parsed, int errors, int duplicates, int suggestions, TimeProvider timeProvider)
    {
        Status = ImportFileStatus.Completed;
        TotalRows = total;
        ParsedRows = parsed;
        ErrorRows = errors;
        DuplicateRows = duplicates;
        SuggestionRows = suggestions;
        CompletedAt = timeProvider.GetUtcNow();
    }

    /// <summary>Marks the parse run as failed and counts the attempt. A failed file can still be retried.</summary>
    public void Fail(string errorMessage, TimeProvider timeProvider)
    {
        Status = ImportFileStatus.Failed;
        ErrorMessage = errorMessage;
        RetryCount++;
        CompletedAt = timeProvider.GetUtcNow();
    }

    /// <summary>Cancels the import for good. No-op once the file has already reached a terminal state.</summary>
    public bool Abort(TimeProvider timeProvider)
    {
        if (IsTerminal) return false;
        Status = ImportFileStatus.Aborted;
        CompletedAt = timeProvider.GetUtcNow();
        return true;
    }

    /// <summary>Resets a failed file back to received so it can be parsed again. No-op unless it failed.</summary>
    public bool Retry(TimeProvider timeProvider)
    {
        if (Status != ImportFileStatus.Failed) return false;
        Status = ImportFileStatus.Received;
        ErrorMessage = null;
        StartedAt = null;
        CompletedAt = null;
        UpdatedAt = timeProvider.GetUtcNow();
        return true;
    }
}
