using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Tars.Core.Localization.Abstractions;

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
    Guid? InstallmentPlanId,
    short? InstallmentNumber,
    string Origin,
    DateTimeOffset? PostedAt,
    DateTimeOffset? VoidedAt,
    string? VoidReason,
    // Language-neutral descriptor for system-defined text (null for user-entered descriptions).
    // Clients may use these to re-localize without another request; Description already comes resolved.
    string? DescriptionKey,
    IReadOnlyList<string>? DescriptionArgs)
{
    /// <summary>Maps without localization — for command results, which only ever carry user text.</summary>
    public static TransactionDto From(Transaction t) => From(t, null);

    /// <summary>
    /// Maps and, for system-defined entries, resolves <see cref="Description"/> in the current culture
    /// via <paramref name="messages"/> (fallback to the stored text).
    /// </summary>
    public static TransactionDto From(Transaction t, IMessageProvider? messages)
    {
        var description = t.SystemDescription is { } sd && messages is not null
            ? messages.Get(sd.Key, t.Description, sd.Args.ToArray())
            : t.Description;

        return new(t.Id, t.AccountId, t.CardStatementId, t.CardId, t.PaidStatementId, t.Kind.Value, t.Status.Value,
            t.Amount, t.Currency.Value, t.OccurredOn, description, t.Payee, t.Notes, t.SystemCategoryId,
            t.UserCategoryId, t.TransferGroupId, t.FxRate, t.InstallmentPlanId, t.InstallmentNumber, t.Origin,
            t.PostedAt, t.VoidedAt, t.VoidReason, t.SystemDescription?.Key, t.SystemDescription?.Args);
    }
}
