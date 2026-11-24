using Pottmayer.Pandora.Modules.Finances.Domain.Errors;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Shared.Domain;
using Pottmayer.Tars.Core.Ddd;
using Pottmayer.Tars.Core.Primitives.Outcomes;

namespace Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;

/// <summary>
/// An asset inside an account (fin022). A <c>unit</c> position keeps a <see cref="Quantity"/> and an
/// <see cref="AverageCost"/> (one open position per asset and account); an <c>application</c> position is
/// one fixed-income application, with the <see cref="InvestedAmount"/> still invested and its terms.
/// Every change goes through <see cref="Apply"/>, which returns the <see cref="PositionMovement"/> that
/// records it; <see cref="Undo"/> reverts the latest one. The position reaches zero → closed.
/// </summary>
public sealed class Position : AggregateRoot<Guid>, IAuditable
{
    public Guid UserId { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid AssetId { get; private set; }
    public AssetBehavior Behavior { get; private set; } = AssetBehavior.Unit;
    public PositionStatus Status { get; private set; } = PositionStatus.Open;
    public decimal? Quantity { get; private set; }
    public decimal? AverageCost { get; private set; }
    public decimal? InvestedAmount { get; private set; }
    public DateOnly? AppliedOn { get; private set; }
    public FixedIncomeIndex? Index { get; private set; }
    public decimal? IndexPercent { get; private set; }
    public decimal? SpreadRate { get; private set; }
    public DateOnly? MaturityOn { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsOpen => Status == PositionStatus.Open;
    public bool IsUnit => Behavior == AssetBehavior.Unit;

    /// <summary>What the position cost: quantity × average cost, or the amount still invested.</summary>
    public decimal CostBasis => IsUnit ? RoundMoney((Quantity ?? 0m) * (AverageCost ?? 0m)) : InvestedAmount ?? 0m;

    /// <summary>
    /// What the position is worth now, worked out at read time like an account's balance (D1): the latest
    /// reported value is the anchor, and the movements recorded after it move it. A buy adds what was
    /// paid, a sale or come-cotas keeps the share of the units left, a redemption takes the gross out.
    /// Without an anchor the position is worth its cost; a closed one is worth nothing.
    /// </summary>
    /// <param name="movements">The position's movements, in any order.</param>
    public decimal CurrentValue(PositionValuation? latest, IEnumerable<PositionMovement> movements)
    {
        if (!IsOpen) return 0m;
        if (latest is null) return CostBasis;

        var anchoredAt = latest.UpdatedAt ?? latest.CreatedAt;
        var value = latest.Value;
        foreach (var m in movements.Where(m => m.CreatedAt > anchoredAt).OrderBy(m => m.CreatedAt).ThenBy(m => m.Id))
        {
            if (m.Kind == PositionMovementKind.Buy)
                value += m.GrossAmount ?? 0m;
            else if ((m.Kind == PositionMovementKind.Sell || m.Kind == PositionMovementKind.ComeCotas) && m.QuantityBefore is > 0m)
                value *= (m.QuantityBefore.Value - (m.Quantity ?? 0m)) / m.QuantityBefore.Value;
            else if (m.Kind == PositionMovementKind.Redemption)
                value = Math.Max(0m, value - (m.GrossAmount ?? 0m));
        }

        return RoundMoney(value);
    }

    private Position() { }

    /// <summary>A unit position, empty until its first movement (opening or buy).</summary>
    public static Position CreateUnit(Guid userId, Guid accountId, Guid assetId, TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            AccountId = accountId,
            AssetId = assetId,
            Behavior = AssetBehavior.Unit,
            Quantity = 0m,
            AverageCost = 0m,
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>A fixed-income application with its terms, empty until its first movement.</summary>
    public static Position CreateApplication(
        Guid userId, Guid accountId, Guid assetId, DateOnly appliedOn, FixedIncomeIndex? index,
        decimal? indexPercent, decimal? spreadRate, DateOnly? maturityOn, TimeProvider timeProvider) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            AccountId = accountId,
            AssetId = assetId,
            Behavior = AssetBehavior.Application,
            InvestedAmount = 0m,
            AppliedOn = appliedOn,
            Index = index,
            IndexPercent = indexPercent,
            SpreadRate = spreadRate,
            MaturityOn = maturityOn,
            CreatedAt = timeProvider.GetUtcNow()
        };

    /// <summary>
    /// Applies a movement and returns it, with the position's previous state recorded for undo.
    /// <paramref name="isFirst"/> says whether the position has no movement yet: the first one must be an
    /// opening or a buy, and an opening can only be first.
    /// </summary>
    public Result<PositionMovement> Apply(MovementInput input, bool isFirst, TimeProvider timeProvider)
    {
        if (!IsOpen) return Fail(PositionErrors.Closed);

        var kind = input.Kind;
        var startsPosition = kind == PositionMovementKind.Opening || kind == PositionMovementKind.Buy;
        if (isFirst && !startsPosition) return Fail(PositionErrors.FirstMovement);
        if (!isFirst && kind == PositionMovementKind.Opening) return Fail(PositionErrors.OpeningNotFirst);
        if (input.Costs < 0m || input.TaxWithheld < 0m) return Fail(PositionErrors.InvalidAmount);

        return IsUnit ? ApplyUnit(input, timeProvider) : ApplyApplication(input, isFirst, timeProvider);
    }

    private Result<PositionMovement> ApplyUnit(MovementInput input, TimeProvider timeProvider)
    {
        var q = Quantity ?? 0m;
        var c = AverageCost ?? 0m;
        var kind = input.Kind;

        if (kind == PositionMovementKind.Opening)
        {
            if (input.Quantity is not > 0m) return Fail(PositionErrors.InvalidQuantity);
            if (input.AverageCost is not >= 0m) return Fail(PositionErrors.InvalidAmount);

            var movement = Record(input, timeProvider, gross: null, net: null, realized: null);
            Quantity = input.Quantity;
            AverageCost = RoundUnit(input.AverageCost.Value);
            return Ok(movement);
        }

        if (kind == PositionMovementKind.Buy)
        {
            if (input.Quantity is not > 0m) return Fail(PositionErrors.InvalidQuantity);
            if (input.UnitPrice is not >= 0m) return Fail(PositionErrors.InvalidAmount);

            var gross = RoundMoney(input.Quantity.Value * input.UnitPrice.Value);
            var total = gross + input.Costs;
            if (total <= 0m) return Fail(PositionErrors.InvalidAmount);

            var movement = Record(input, timeProvider, gross, net: total, realized: null);
            var newQuantity = q + input.Quantity.Value;
            AverageCost = RoundUnit((q * c + input.Quantity.Value * input.UnitPrice.Value + input.Costs) / newQuantity);
            Quantity = newQuantity;
            return Ok(movement);
        }

        if (kind == PositionMovementKind.Sell)
        {
            if (input.Quantity is not > 0m || input.Quantity > q) return Fail(PositionErrors.InvalidQuantity);
            if (input.UnitPrice is not >= 0m) return Fail(PositionErrors.InvalidAmount);

            var gross = RoundMoney(input.Quantity.Value * input.UnitPrice.Value);
            var net = gross - input.Costs - input.TaxWithheld;
            if (net < 0m) return Fail(PositionErrors.NegativeNet);

            var realized = gross - input.Costs - RoundMoney(input.Quantity.Value * c);
            var movement = Record(input, timeProvider, gross, net, realized);
            Quantity = q - input.Quantity.Value;
            if (Quantity == 0m) Close(timeProvider);
            return Ok(movement);
        }

        if (kind == PositionMovementKind.Income) return ApplyIncome(input, timeProvider);

        if (kind == PositionMovementKind.Split)
        {
            if (input.SplitFactor is not > 0m || input.SplitFactor == 1m) return Fail(PositionErrors.InvalidSplitFactor);

            var movement = Record(input, timeProvider, gross: null, net: null, realized: null);
            Quantity = q * input.SplitFactor.Value;
            AverageCost = RoundUnit(c / input.SplitFactor.Value);
            return Ok(movement);
        }

        if (kind == PositionMovementKind.ComeCotas)
        {
            // Come-cotas takes part of the quotas to pay the tax; it never empties the position.
            if (input.Quantity is not > 0m || input.Quantity >= q) return Fail(PositionErrors.InvalidQuantity);

            var movement = Record(input, timeProvider, gross: null, net: null, realized: null);
            Quantity = q - input.Quantity.Value;
            return Ok(movement);
        }

        return Fail(PositionErrors.KindNotAllowed(kind.Value, Behavior.Value));
    }

    private Result<PositionMovement> ApplyApplication(MovementInput input, bool isFirst, TimeProvider timeProvider)
    {
        var v = InvestedAmount ?? 0m;
        var kind = input.Kind;

        if (kind == PositionMovementKind.Opening || kind == PositionMovementKind.Buy)
        {
            // A new application is always a new position: a buy only starts one.
            if (!isFirst) return Fail(PositionErrors.KindNotAllowed(kind.Value, Behavior.Value));
            if (input.InvestedAmount is not > 0m) return Fail(PositionErrors.InvalidAmount);

            var invested = RoundMoney(input.InvestedAmount.Value);
            var movement = kind == PositionMovementKind.Buy
                ? Record(input, timeProvider, gross: invested, net: invested + input.Costs, realized: null)
                : Record(input, timeProvider, gross: null, net: null, realized: null);
            InvestedAmount = invested;
            return Ok(movement);
        }

        if (kind == PositionMovementKind.Redemption)
        {
            if (input.GrossAmount is not > 0m) return Fail(PositionErrors.InvalidAmount);
            if (input.CurrentValue is not > 0m) return Fail(PositionErrors.CurrentValueRequired);

            var gross = RoundMoney(input.GrossAmount.Value);
            var net = gross - input.TaxWithheld - input.Costs;
            if (net < 0m) return Fail(PositionErrors.NegativeNet);

            // The share of the position redeemed takes the same share of what was invested.
            // A gross at or above the current value redeems everything.
            var share = Math.Min(1m, gross / input.CurrentValue.Value);
            var basis = share == 1m ? v : RoundMoney(v * share);
            var movement = Record(input, timeProvider, gross, net, realized: gross - basis);
            InvestedAmount = v - basis;
            if (InvestedAmount == 0m) Close(timeProvider);
            return Ok(movement);
        }

        if (kind == PositionMovementKind.Maturity)
        {
            if (input.GrossAmount is not > 0m) return Fail(PositionErrors.InvalidAmount);

            var gross = RoundMoney(input.GrossAmount.Value);
            var net = gross - input.TaxWithheld - input.Costs;
            if (net < 0m) return Fail(PositionErrors.NegativeNet);

            var movement = Record(input, timeProvider, gross, net, realized: gross - v);
            InvestedAmount = 0m;
            Close(timeProvider);
            return Ok(movement);
        }

        if (kind == PositionMovementKind.Income) return ApplyIncome(input, timeProvider);

        return Fail(PositionErrors.KindNotAllowed(kind.Value, Behavior.Value));
    }

    private Result<PositionMovement> ApplyIncome(MovementInput input, TimeProvider timeProvider)
    {
        if (input.IncomeKind is null) return Fail(PositionErrors.IncomeKindRequired);
        if (input.GrossAmount is not > 0m) return Fail(PositionErrors.InvalidAmount);

        var gross = RoundMoney(input.GrossAmount.Value);
        var net = gross - input.TaxWithheld - input.Costs;
        if (net < 0m) return Fail(PositionErrors.NegativeNet);

        return Ok(Record(input, timeProvider, gross, net, realized: null));
    }

    /// <summary>
    /// Reverts a movement: the position goes back to the state recorded in it and reopens. The caller
    /// passes only the position's latest movement (and deletes the position when it was the only one).
    /// </summary>
    public Result<bool> Undo(PositionMovement movement)
    {
        if (movement.PositionId != Id) return Result<bool>.Failure([PositionErrors.NotLatestMovement]);

        Quantity = movement.QuantityBefore;
        AverageCost = movement.AverageCostBefore;
        InvestedAmount = movement.InvestedAmountBefore;
        Status = PositionStatus.Open;
        ClosedAt = null;
        return Result<bool>.Success(true);
    }

    private PositionMovement Record(MovementInput input, TimeProvider timeProvider, decimal? gross, decimal? net, decimal? realized) =>
        PositionMovement.Create(this, input, timeProvider, gross, net, realized);

    private void Close(TimeProvider timeProvider)
    {
        Status = PositionStatus.Closed;
        ClosedAt = timeProvider.GetUtcNow();
    }

    private static Result<PositionMovement> Fail(Error error) => Result<PositionMovement>.Failure([error]);

    private static Result<PositionMovement> Ok(PositionMovement movement) => Result<PositionMovement>.Success(movement);

    /// <summary>Money is kept in cents, half away from zero.</summary>
    public static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Per-unit prices and costs keep 10 decimals, as the columns do.</summary>
    public static decimal RoundUnit(decimal value) => Math.Round(value, 10, MidpointRounding.AwayFromZero);
}
