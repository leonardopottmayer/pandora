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

    public static ImportFile Create(
        Guid userId,
        Guid? layoutId,
        Guid? accountId,
        Guid? cardId,
        string fileName,
        string fileHash,
        byte[] fileContent,
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
            Status = ImportFileStatus.Received,
            CreatedAt = timeProvider.GetUtcNow()
        };
    }

    public bool StartParsing(TimeProvider timeProvider)
    {
        if (!IsReceived) return false;
        Status = ImportFileStatus.Parsing;
        StartedAt = timeProvider.GetUtcNow();
        return true;
    }

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

    public void Fail(string errorMessage, TimeProvider timeProvider)
    {
        Status = ImportFileStatus.Failed;
        ErrorMessage = errorMessage;
        RetryCount++;
        CompletedAt = timeProvider.GetUtcNow();
    }

    public bool Abort(TimeProvider timeProvider)
    {
        if (IsTerminal) return false;
        Status = ImportFileStatus.Aborted;
        CompletedAt = timeProvider.GetUtcNow();
        return true;
    }

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
