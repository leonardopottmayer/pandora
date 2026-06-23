using Pottmayer.Pandora.Modules.Finances.Application.Dtos;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Finances.Application.Services;

internal static class InstallmentPlanAssembler
{
    /// <summary>
    /// Builds the read model for a plan: each installment with the state of its statement, plus the
    /// remaining amount (still owed on not-yet-paid statements) and how many installments are paid.
    /// </summary>
    public static InstallmentPlanDto Assemble(
        InstallmentPlan plan,
        IReadOnlyList<Transaction> installments,
        IReadOnlyDictionary<Guid, CardStatement> statementsById)
    {
        var items = new List<InstallmentItemDto>(installments.Count);
        var remaining = 0m;
        var paid = 0;

        foreach (var tx in installments.OrderBy(t => t.InstallmentNumber))
        {
            var statement = tx.CardStatementId is not null && statementsById.TryGetValue(tx.CardStatementId.Value, out var s) ? s : null;
            var statementStatus = statement?.Status ?? StatementStatus.Open;

            items.Add(new InstallmentItemDto(
                tx.InstallmentNumber ?? 0,
                tx.Id,
                tx.CardStatementId ?? Guid.Empty,
                statement?.ReferenceMonth ?? string.Empty,
                tx.Amount,
                tx.Status.Value,
                statementStatus.Value));

            if (tx.IsVoid) continue;
            if (statementStatus == StatementStatus.Paid) paid++;
            else remaining += tx.Amount;
        }

        var first = installments.FirstOrDefault();

        return new InstallmentPlanDto(
            plan.Id,
            plan.CardId,
            plan.Origin.Value,
            plan.Description,
            plan.InstallmentCount,
            plan.TotalAmount,
            plan.TotalIsEstimate,
            plan.FirstReferenceMonth,
            remaining,
            paid,
            first?.SystemCategoryId,
            first?.UserCategoryId,
            items);
    }
}
