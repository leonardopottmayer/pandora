using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

namespace Pottmayer.Pandora.Modules.Finances.Application.Dtos;

public sealed record TransactionDto(
    Guid Id,
    Guid? AccountId,
    Guid? CardStatementId,
    Guid? CardId,
    Guid? PaidStatementId,
    string Kind,
    string Status,
    decimal Amount,
    string Currency,
    DateOnly OccurredOn,
    string Description,
    string? Payee,
    string? Notes,
    Guid? SystemCategoryId,
    Guid? UserCategoryId,
    Guid? TransferGroupId,
    decimal? FxRate,
    string Origin,
    DateTimeOffset? PostedAt,
    DateTimeOffset? VoidedAt,
    string? VoidReason)
{
    public static TransactionDto From(Transaction t) =>
        new(t.Id, t.AccountId, t.CardStatementId, t.CardId, t.PaidStatementId, t.Kind.Value, t.Status.Value,
            t.Amount, t.Currency.Value, t.OccurredOn, t.Description, t.Payee, t.Notes, t.SystemCategoryId,
            t.UserCategoryId, t.TransferGroupId, t.FxRate, t.Origin, t.PostedAt, t.VoidedAt, t.VoidReason);
}
