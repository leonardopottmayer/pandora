using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// What a <see cref="Position"/> is worth at a date (fin024), as reported by the user, imported, or
/// later generated from quotes. One per position and date; the latest is the current value.
/// </summary>
public sealed class PositionValuation : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public Guid PositionId { get; private set; }
    public DateOnly ValuedOn { get; private set; }
    public decimal Value { get; private set; }
    public ValuationSource Source { get; private set; } = ValuationSource.Manual;

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private PositionValuation() { }

    public static PositionValuation Create(
        Guid userId, Guid positionId, DateOnly valuedOn, decimal value, ValuationSource source, TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            PositionId = positionId,
            ValuedOn = valuedOn,
            Value = Position.RoundMoney(value),
            Source = source,
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>Replaces the value reported for the same date.</summary>
    public void Replace(decimal value, ValuationSource source)
    {
        Value = Position.RoundMoney(value);
        Source = source;
    }
}
