using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

public sealed record ImportFileDto(
    Guid Id,
    Guid UserId,
    Guid? LayoutId,
    Guid? AccountId,
    Guid? CardId,
    string FileName,
    string FileHash,
    int FileSize,
    Guid CorrelationId,
    DateOnly? CutoffDate,
    string Status,
    int TotalRows,
    int ParsedRows,
    int ErrorRows,
    int DuplicateRows,
    int SuggestionRows,
    int RetryCount,
    string? ErrorMessage,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt)
{
    public static ImportFileDto From(ImportFile f) => new(
        f.Id,
        f.UserId,
        f.LayoutId,
        f.AccountId,
        f.CardId,
        f.FileName,
        f.FileHash,
        f.FileSize,
        f.CorrelationId,
        f.CutoffDate,
        f.Status.Value,
        f.TotalRows,
        f.ParsedRows,
        f.ErrorRows,
        f.DuplicateRows,
        f.SuggestionRows,
        f.RetryCount,
        f.ErrorMessage,
        f.StartedAt,
        f.CompletedAt,
        f.CreatedAt);
}
