using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// Something that happened to a <see cref="Position"/> (fin023). Created only by
/// <see cref="Position.Apply"/>, which also records the position as it was (<c>*Before</c>), so the
/// latest movement can be undone. A movement that moves money owns one ledger entry on the account
/// (<see cref="TransactionId"/>), of the kind <see cref="LedgerKind"/> says.
/// </summary>
public sealed class PositionMovement : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public Guid PositionId { get; private set; }
    public PositionMovementKind Kind { get; private set; } = PositionMovementKind.Opening;
    public DateOnly OccurredOn { get; private set; }
    public decimal? Quantity { get; private set; }
    public decimal? UnitPrice { get; private set; }
    public decimal? GrossAmount { get; private set; }
    public decimal Costs { get; private set; }
    public decimal TaxWithheld { get; private set; }
    /// <summary>What reached or left the cash; its direction comes from <see cref="LedgerKind"/>.</summary>
    public decimal? NetAmount { get; private set; }
    public IncomeKind? IncomeKind { get; private set; }
    public decimal? SplitFactor { get; private set; }
    public decimal? RealizedResult { get; private set; }
    public decimal? QuantityBefore { get; private set; }
    public decimal? AverageCostBefore { get; private set; }
    public decimal? InvestedAmountBefore { get; private set; }
    public Guid? TransactionId { get; private set; }
    public string? Notes { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    private PositionMovement() { }

    internal static PositionMovement Create(
        Position position, MovementInput input, TimeProvider timeProvider,
        decimal? grossAmount, decimal? netAmount, decimal? realizedResult) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = position.UserId,
            PositionId = position.Id,
            Kind = input.Kind,
            OccurredOn = input.OccurredOn,
            Quantity = input.Quantity,
            UnitPrice = input.UnitPrice,
            GrossAmount = grossAmount,
            Costs = input.Costs,
            TaxWithheld = input.TaxWithheld,
            NetAmount = netAmount,
            IncomeKind = input.IncomeKind,
            SplitFactor = input.SplitFactor,
            RealizedResult = realizedResult,
            QuantityBefore = position.Quantity,
            AverageCostBefore = position.AverageCost,
            InvestedAmountBefore = position.InvestedAmount,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>
    /// The ledger entry this movement creates on the account, or null when it moves no money
    /// (opening, split, come-cotas).
    /// </summary>
    public TransactionKind? LedgerKind =>
        Kind == PositionMovementKind.Buy ? TransactionKind.InvestmentContribution
        : Kind == PositionMovementKind.Sell || Kind == PositionMovementKind.Redemption || Kind == PositionMovementKind.Maturity
            ? TransactionKind.InvestmentRedemption
        : Kind == PositionMovementKind.Income ? TransactionKind.Yield
        : null;

    /// <summary>Records the ledger entry created for this movement.</summary>
    public void LinkTransaction(Guid transactionId) => TransactionId = transactionId;
}

/// <summary>
/// What the user tells about a movement. Which fields matter depends on the kind and on the position's
/// behavior; <see cref="Position.Apply"/> checks them.
/// </summary>
/// <param name="AverageCost">Opening of a unit position: the average cost per unit.</param>
/// <param name="InvestedAmount">Opening or buy of an application: the amount invested.</param>
/// <param name="CurrentValue">Redemption of an application: the position's value the share is taken from.</param>
public sealed record MovementInput(
    PositionMovementKind Kind,
    DateOnly OccurredOn,
    decimal? Quantity = null,
    decimal? UnitPrice = null,
    decimal? GrossAmount = null,
    decimal Costs = 0m,
    decimal TaxWithheld = 0m,
    IncomeKind? IncomeKind = null,
    decimal? SplitFactor = null,
    decimal? AverageCost = null,
    decimal? InvestedAmount = null,
    decimal? CurrentValue = null,
    string? Notes = null);
