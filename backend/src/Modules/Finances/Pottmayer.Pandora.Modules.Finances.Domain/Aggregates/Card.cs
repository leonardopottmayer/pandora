using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

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

    public void Archive(TimeProvider timeProvider)
    {
        if (IsArchived) return;
        ArchivedAt = timeProvider.GetUtcNow();
    }

    public void Unarchive()
    {
        ArchivedAt = null;
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
