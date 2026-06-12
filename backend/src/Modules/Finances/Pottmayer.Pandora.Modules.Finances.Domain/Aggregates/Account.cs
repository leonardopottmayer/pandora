using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// A balance repository owned by the user (fin001): wallet/cash, checking, savings, international,
/// crypto, investment. The <see cref="Currency"/> is fixed at creation — there is no mutator for it,
/// so it can never change. An archived account keeps its history but rejects business mutations.
/// Balance is not stored here; it is derived from the ledger (phase 04).
/// </summary>
public sealed class Account : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public AccountType Type { get; private set; } = AccountType.Other;
    public CurrencyCode Currency { get; private set; } = null!;
    public string? Institution { get; private set; }
    public string? Description { get; private set; }
    public string? Color { get; private set; }
    public string? Icon { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsArchived => ArchivedAt is not null;

    private Account() { }

    public static Account Create(
        Guid userId,
        string name,
        AccountType type,
        CurrencyCode currency,
        string? institution,
        string? description,
        string? color,
        string? icon,
        int displayOrder,
        TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = name.Trim(),
            Type = type,
            Currency = currency,
            Institution = institution,
            Description = description,
            Color = color,
            Icon = icon,
            DisplayOrder = displayOrder,
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>
    /// Edits the mutable configuration. Type and currency are intentionally absent: both are fixed
    /// at creation. Returns <c>false</c> if the account is archived (no business mutation allowed).
    /// </summary>
    public bool Update(string name, AccountType type, string? institution, string? description,
        string? color, string? icon, int displayOrder)
    {
        if (IsArchived) return false;

        Name = name.Trim();
        Type = type;
        Institution = institution;
        Description = description;
        Color = color;
        Icon = icon;
        DisplayOrder = displayOrder;
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
}
