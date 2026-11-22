using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;

/// <summary>
/// A per-occurrence deviation from an <see cref="Event"/> series (doc agd003). Its natural key is
/// <c>(EventId, OriginalStartsAt)</c> — which occurrence, identified by its on-grid start. It is
/// <see cref="IsCancelled"/> (the EXDATE case, the occurrence disappears) or an edit, where the
/// non-null columns override the series for that one occurrence.
///
/// <para>Modeled as its own aggregate root (not an EF child navigation) so a range query can load the
/// overrides for many events in one shot, mirroring how the dispatch ledgers are queried.</para>
/// </summary>
public sealed class EventOccurrenceOverride : AggregateRoot<Guid>, IAuditable
{
    public Guid EventId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>The on-grid start of the occurrence this override replaces. Half of the natural key.</summary>
    public DateTimeOffset OriginalStartsAt { get; private set; }

    /// <summary>The EXDATE case: this occurrence is removed from the series.</summary>
    public bool IsCancelled { get; private set; }

    public DateTimeOffset? StartsAt { get; private set; }
    public DateTimeOffset? EndsAt { get; private set; }
    public string? Title { get; private set; }
    public string? Description { get; private set; }
    public string? Location { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private EventOccurrenceOverride() { }

    public static EventOccurrenceOverride Create(
        Guid eventId, Guid userId, DateTimeOffset originalStartsAt, TimeProvider timeProvider) => new()
        {
            Id = Guid.CreateVersion7(),
            EventId = eventId,
            UserId = userId,
            OriginalStartsAt = originalStartsAt.ToUniversalTime(),
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>Marks the occurrence cancelled (an EXDATE). Clears any edit fields it carried.</summary>
    public void Cancel()
    {
        IsCancelled = true;
        StartsAt = null;
        EndsAt = null;
        Title = null;
        Description = null;
        Location = null;
    }

    /// <summary>Sets the per-occurrence edit fields. A null field falls back to the series value on read.</summary>
    public void Edit(
        DateTimeOffset? startsAt, DateTimeOffset? endsAt, string? title, string? description, string? location)
    {
        IsCancelled = false;
        StartsAt = startsAt?.ToUniversalTime();
        EndsAt = endsAt?.ToUniversalTime();
        Title = title;
        Description = description;
        Location = location;
    }
}
