using System.Text.RegularExpressions;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// A card purchase split into <see cref="InstallmentCount"/> installments (fin009): one plan plus N
/// transactions, one per consecutive statement. In this phase only <c>manual</c> plans exist, whose
/// installments sum exactly to <see cref="TotalAmount"/>; import-inferred plans (estimated total,
/// projected future installments) arrive in phase 10. The plan is a thin record — the per-installment
/// state lives on the <see cref="Transaction"/> rows that carry its id.
/// </summary>
public sealed partial class InstallmentPlan : AggregateRoot<Guid>, IAuditable
{
    public const int MinInstallments = 2;

    public Guid UserId { get; private set; }
    public Guid CardId { get; private set; }

    /// <summary>Provenance. Only <c>manual</c> exists in this phase (<c>import</c> arrives in phase 10).</summary>
    public string Origin { get; private set; } = "manual";

    public decimal TotalAmount { get; private set; }
    public bool TotalIsEstimate { get; private set; }
    public int InstallmentCount { get; private set; }

    /// <summary>Reference month (<c>yyyy-MM</c>) of the first installment's statement.</summary>
    public string FirstReferenceMonth { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    /// <summary>The description stripped of its installment marker — the matching key for phase 10.</summary>
    public string NormalizedDescription { get; private set; } = string.Empty;

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private InstallmentPlan() { }

    public static InstallmentPlan CreateManual(
        Guid userId,
        Guid cardId,
        decimal totalAmount,
        int installmentCount,
        string firstReferenceMonth,
        string description,
        TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CardId = cardId,
            Origin = "manual",
            TotalAmount = totalAmount,
            TotalIsEstimate = false,
            InstallmentCount = installmentCount,
            FirstReferenceMonth = firstReferenceMonth,
            Description = description.Trim(),
            NormalizedDescription = NormalizeDescription(description),
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>
    /// Splits <paramref name="total"/> into <paramref name="count"/> installments rounded to cents,
    /// putting any rounding remainder on the first installment so the parts sum back to the total
    /// exactly (e.g. 1000.00 in 3x → 333.34 / 333.33 / 333.33).
    /// </summary>
    public static decimal[] SplitAmount(decimal total, int count)
    {
        var even = Math.Truncate(total / count * 100m) / 100m;
        var parts = new decimal[count];
        for (var i = 1; i < count; i++) parts[i] = even;
        parts[0] = total - even * (count - 1);
        return parts;
    }

    /// <summary>
    /// Strips a trailing installment marker (<c>3/12</c>, <c>03/12</c>, <c>PARC 3/12</c>, <c>3 de 12</c>)
    /// and collapses whitespace, producing a stable key for matching imported installments to a plan.
    /// </summary>
    public static string NormalizeDescription(string description)
    {
        var withoutMarker = InstallmentMarker().Replace(description, string.Empty);
        return Whitespace().Replace(withoutMarker, " ").Trim().ToLowerInvariant();
    }

    [GeneratedRegex(@"\b(parc(ela)?\.?\s*)?\d{1,2}\s*(/|\s+de\s+)\s*\d{1,2}\b", RegexOptions.IgnoreCase)]
    private static partial Regex InstallmentMarker();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
