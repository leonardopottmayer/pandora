using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// One of a <see cref="Card"/>'s numbers: the physical card or a virtual one. All of them bill on the
/// card's statements. A number a transaction used is archived rather than deleted, so the history
/// keeps pointing at it.
/// </summary>
public sealed class CardNumber : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public Guid CardId { get; private set; }
    public CardNumberKind Kind { get; private set; } = CardNumberKind.Physical;
    public string LastFour { get; private set; } = string.Empty;
    public string? Nickname { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsArchived => ArchivedAt is not null;

    private CardNumber() { }

    /// <summary>Adds a number to a card.</summary>
    public static CardNumber Create(
        Guid userId, Guid cardId, CardNumberKind kind, string lastFour, string? nickname, TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CardId = cardId,
            Kind = kind,
            LastFour = lastFour.Trim(),
            Nickname = Normalize(nickname),
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>Edits the number. Returns <c>false</c> if it is archived.</summary>
    public bool Update(CardNumberKind kind, string lastFour, string? nickname)
    {
        if (IsArchived) return false;

        Kind = kind;
        LastFour = lastFour.Trim();
        Nickname = Normalize(nickname);
        return true;
    }

    /// <summary>Retires the number while keeping it for the transactions that used it. No-op if already archived.</summary>
    public void Archive(TimeProvider timeProvider)
    {
        if (IsArchived) return;
        ArchivedAt = timeProvider.GetUtcNow();
    }

    /// <summary>True when <paramref name="value"/> is exactly four digits (surrounding spaces allowed).</summary>
    public static bool IsValidLastFour(string? value) =>
        value?.Trim() is { Length: 4 } trimmed && trimmed.All(char.IsAsciiDigit);

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
