using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

public sealed record ImportRowDto(
    Guid Id,
    Guid ImportFileId,
    int RowIndex,
    string RawData,
    string? ParsedPayload,
    string? ExternalId,
    string? DedupKey,
    string DedupStatus,
    Guid? MatchedTransactionId,
    Guid? MatchedPendingTransactionId,
    short? InstallmentNumber,
    short? InstallmentCount,
    Guid? MatchedInstallmentPlanId,
    Guid? PendingTransactionId,
    string Status,
    string? ErrorMessage,
    DateTimeOffset CreatedAt)
{
    public static ImportRowDto From(ImportRow r) => new(
        r.Id,
        r.ImportFileId,
        r.RowIndex,
        r.RawData,
        r.ParsedPayload,
        r.ExternalId,
        r.DedupKey,
        r.DedupStatus.Value,
        r.MatchedTransactionId,
        r.MatchedPendingTransactionId,
        r.InstallmentNumber,
        r.InstallmentCount,
        r.MatchedInstallmentPlanId,
        r.PendingTransactionId,
        r.Status.Value,
        r.ErrorMessage,
        r.CreatedAt);
}
