using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

public sealed record PendingTransactionDto(
    Guid Id,
    string Source,
    Guid? RecurringTransactionId,
    Guid? AccountId,
    Guid? CardId,
    string Kind,
    decimal? Amount,
    string Currency,
    DateOnly OccurredOn,
    string Description,
    string? Payee,
    string? Notes,
    Guid? SystemCategoryId,
    Guid? UserCategoryId,
    Guid? SuggestedStatementId,
    string OriginalPayload,
    string Status,
    DateTimeOffset? DecidedAt,
    Guid? DecidedBy,
    string? RejectionReason,
    Guid? TransactionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    public static PendingTransactionDto From(PendingTransaction p) => new(
        p.Id,
        p.Source,
        p.RecurringTransactionId,
        p.AccountId,
        p.CardId,
        p.Kind,
        p.Amount,
        p.Currency,
        p.OccurredOn,
        p.Description,
        p.Payee,
        p.Notes,
        p.SystemCategoryId,
        p.UserCategoryId,
        p.SuggestedStatementId,
        p.OriginalPayload,
        p.Status,
        p.DecidedAt,
        p.DecidedBy,
        p.RejectionReason,
        p.TransactionId,
        p.CreatedAt,
        p.UpdatedAt);
}
