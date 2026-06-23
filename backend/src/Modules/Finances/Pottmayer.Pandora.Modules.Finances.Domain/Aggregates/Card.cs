using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// A credit card owned by the user, billed through monthly <see cref="CardStatement"/>s rather than
/// posting directly to an account. <see cref="Currency"/> is fixed at creation. An archived card
/// keeps its history but rejects business mutations.
/// </summary>
public sealed class Card : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Brand { get; private set; }
    public string? LastFour { get; private set; }
    public decimal? CreditLimit { get; private set; }
    public int ClosingDay { get; private set; }
    public int DueDay { get; private set; }
    public CurrencyCode Currency { get; private set; } = null!;
    public Guid? DefaultPaymentAccountId { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsArchived => ArchivedAt is not null;

    private Card() { }

    /// <summary>Registers a new card for the user with its currency fixed for life.</summary>
    public static Card Create(
        Guid userId,
        string name,
        string? brand,
        string? lastFour,
        decimal? creditLimit,
        int closingDay,
        int dueDay,
        CurrencyCode currency,
        Guid? defaultPaymentAccountId,
        TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = name.Trim(),
            Brand = Normalize(brand),
            LastFour = Normalize(lastFour),
            CreditLimit = creditLimit,
            ClosingDay = closingDay,
            DueDay = dueDay,
            Currency = currency,
            DefaultPaymentAccountId = defaultPaymentAccountId,
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>
    /// Edits the mutable configuration. Currency is intentionally absent: it is fixed at creation.
    /// Returns <c>false</c> if the card is archived (no business mutation allowed).
    /// </summary>
    public bool Update(
        string name,
        string? brand,
        string? lastFour,
        decimal? creditLimit,
        int closingDay,
        int dueDay,
        Guid? defaultPaymentAccountId)
    {
        if (IsArchived) return false;

        Name = name.Trim();
        Brand = Normalize(brand);
        LastFour = Normalize(lastFour);
        CreditLimit = creditLimit;
        ClosingDay = closingDay;
        DueDay = dueDay;
        DefaultPaymentAccountId = defaultPaymentAccountId;
        return true;
    }

    /// <summary>Retires the card from active use while preserving its history. No-op if already archived.</summary>
    public void Archive(TimeProvider timeProvider)
    {
        if (IsArchived) return;
        ArchivedAt = timeProvider.GetUtcNow();
    }

    /// <summary>Brings an archived card back into active use.</summary>
    public void Unarchive()
    {
        ArchivedAt = null;
    }

    /// <summary>Trims a free-text field down to <c>null</c> when it is blank.</summary>
    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
