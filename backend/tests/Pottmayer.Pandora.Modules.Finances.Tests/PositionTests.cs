using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Finances.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Finances.Tests;

public sealed class PositionTests
{
    private static readonly FixedTimeProvider Time = new(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero));
    private static readonly DateOnly Day = new(2026, 11, 23);

    private static Position Unit() => Position.CreateUnit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Time);

    private static Position Application() => Position.CreateApplication(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Day, FixedIncomeIndex.Cdi, 110m, null, new DateOnly(2028, 1, 3), Time);

    private static PositionMovement Ok(Position position, MovementInput input, bool isFirst = false)
    {
        var result = position.Apply(input, isFirst, Time);
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Code)));
        return result.Value!;
    }

    private static string Fails(Position position, MovementInput input, bool isFirst = false)
    {
        var result = position.Apply(input, isFirst, Time);
        Assert.True(result.IsFailure);
        return result.Errors.Single().Code;
    }

    private static Position OpenedUnit(decimal quantity = 100m, decimal averageCost = 10m)
    {
        var position = Unit();
        Ok(position, new MovementInput(PositionMovementKind.Opening, Day, Quantity: quantity, AverageCost: averageCost), isFirst: true);
        return position;
    }

    private static Position OpenedApplication(decimal invested = 1000m)
    {
        var position = Application();
        Ok(position, new MovementInput(PositionMovementKind.Opening, Day, InvestedAmount: invested), isFirst: true);
        return position;
    }

    [Fact]
    public void Opening_sets_quantity_and_average_cost_without_cash()
    {
        var position = Unit();

        var movement = Ok(position, new MovementInput(PositionMovementKind.Opening, Day, Quantity: 100m, AverageCost: 10.5m), isFirst: true);

        Assert.Equal(100m, position.Quantity);
        Assert.Equal(10.5m, position.AverageCost);
        Assert.Null(movement.NetAmount);
        Assert.Null(movement.LedgerKind);
        Assert.Equal(0m, movement.QuantityBefore);
    }

    [Fact]
    public void First_movement_must_open_and_opening_only_comes_first()
    {
        Assert.Equal("Positions.FirstMovement",
            Fails(Unit(), new MovementInput(PositionMovementKind.Sell, Day, Quantity: 1m, UnitPrice: 1m), isFirst: true));
        Assert.Equal("Positions.OpeningNotFirst",
            Fails(OpenedUnit(), new MovementInput(PositionMovementKind.Opening, Day, Quantity: 1m, AverageCost: 1m)));
    }

    [Fact]
    public void Buy_averages_the_cost_with_costs_included()
    {
        var position = OpenedUnit(quantity: 100m, averageCost: 10m);

        var movement = Ok(position, new MovementInput(PositionMovementKind.Buy, Day, Quantity: 100m, UnitPrice: 12m, Costs: 10m));

        // (100·10 + 100·12 + 10) / 200 = 11.05
        Assert.Equal(200m, position.Quantity);
        Assert.Equal(11.05m, position.AverageCost);
        Assert.Equal(1210m, movement.NetAmount);
        Assert.Equal(TransactionKind.InvestmentContribution, movement.LedgerKind);
    }

    [Fact]
    public void Sell_keeps_the_average_cost_and_records_the_realized_result()
    {
        var position = OpenedUnit(quantity: 100m, averageCost: 10m);

        var movement = Ok(position, new MovementInput(PositionMovementKind.Sell, Day, Quantity: 40m, UnitPrice: 15m, Costs: 5m, TaxWithheld: 0.03m));

        // gross 600; net 600 − 5 − 0.03; realized 600 − 5 − 40·10
        Assert.Equal(60m, position.Quantity);
        Assert.Equal(10m, position.AverageCost);
        Assert.Equal(600m, movement.GrossAmount);
        Assert.Equal(594.97m, movement.NetAmount);
        Assert.Equal(195m, movement.RealizedResult);
        Assert.Equal(TransactionKind.InvestmentRedemption, movement.LedgerKind);
        Assert.True(position.IsOpen);
    }

    [Fact]
    public void Selling_everything_closes_and_selling_more_fails()
    {
        var position = OpenedUnit(quantity: 10m);

        Assert.Equal("Positions.InvalidQuantity",
            Fails(position, new MovementInput(PositionMovementKind.Sell, Day, Quantity: 11m, UnitPrice: 1m)));

        Ok(position, new MovementInput(PositionMovementKind.Sell, Day, Quantity: 10m, UnitPrice: 1m));

        Assert.False(position.IsOpen);
        Assert.NotNull(position.ClosedAt);
        Assert.Equal("Positions.Closed",
            Fails(position, new MovementInput(PositionMovementKind.Buy, Day, Quantity: 1m, UnitPrice: 1m)));
    }

    [Fact]
    public void Income_goes_to_cash_net_of_tax_and_needs_its_kind()
    {
        var position = OpenedUnit();

        Assert.Equal("Positions.IncomeKindRequired",
            Fails(position, new MovementInput(PositionMovementKind.Income, Day, GrossAmount: 50m)));

        var movement = Ok(position, new MovementInput(PositionMovementKind.Income, Day, GrossAmount: 50m, TaxWithheld: 7.5m, IncomeKind: IncomeKind.InterestOnEquity));

        Assert.Equal(42.5m, movement.NetAmount);
        Assert.Equal(TransactionKind.Yield, movement.LedgerKind);
        Assert.Equal(100m, position.Quantity);
    }

    [Fact]
    public void Split_multiplies_quantity_and_divides_cost()
    {
        var position = OpenedUnit(quantity: 100m, averageCost: 30m);

        Ok(position, new MovementInput(PositionMovementKind.Split, Day, SplitFactor: 3m));
        Assert.Equal(300m, position.Quantity);
        Assert.Equal(10m, position.AverageCost);

        Ok(position, new MovementInput(PositionMovementKind.Split, Day, SplitFactor: 0.1m));
        Assert.Equal(30m, position.Quantity);
        Assert.Equal(100m, position.AverageCost);

        Assert.Equal("Positions.InvalidSplitFactor", Fails(position, new MovementInput(PositionMovementKind.Split, Day, SplitFactor: 1m)));
    }

    [Fact]
    public void Come_cotas_takes_quotas_but_never_empties_the_position()
    {
        var position = OpenedUnit(quantity: 1000m, averageCost: 2m);

        var movement = Ok(position, new MovementInput(PositionMovementKind.ComeCotas, Day, Quantity: 12.5m, TaxWithheld: 30m));

        Assert.Equal(987.5m, position.Quantity);
        Assert.Equal(2m, position.AverageCost);
        Assert.Null(movement.LedgerKind);
        Assert.Equal("Positions.InvalidQuantity",
            Fails(position, new MovementInput(PositionMovementKind.ComeCotas, Day, Quantity: 987.5m)));
    }

    [Fact]
    public void Undo_restores_the_state_before_the_movement_and_reopens()
    {
        var position = OpenedUnit(quantity: 10m, averageCost: 5m);
        var sell = Ok(position, new MovementInput(PositionMovementKind.Sell, Day, Quantity: 10m, UnitPrice: 8m));
        Assert.False(position.IsOpen);

        Assert.True(position.Undo(sell).IsSuccess);

        Assert.True(position.IsOpen);
        Assert.Null(position.ClosedAt);
        Assert.Equal(10m, position.Quantity);
        Assert.Equal(5m, position.AverageCost);
    }

    [Fact]
    public void Undo_rejects_a_movement_of_another_position()
    {
        var other = OpenedUnit();
        var movement = Ok(other, new MovementInput(PositionMovementKind.Split, Day, SplitFactor: 2m));

        Assert.True(OpenedUnit().Undo(movement).IsFailure);
    }

    [Fact]
    public void Application_buy_moves_cash_and_only_starts_a_position()
    {
        var position = Application();

        var movement = Ok(position, new MovementInput(PositionMovementKind.Buy, Day, InvestedAmount: 5000m, Costs: 0m), isFirst: true);

        Assert.Equal(5000m, position.InvestedAmount);
        Assert.Equal(5000m, movement.NetAmount);
        Assert.Equal(TransactionKind.InvestmentContribution, movement.LedgerKind);
        Assert.Equal("Positions.KindNotAllowed",
            Fails(position, new MovementInput(PositionMovementKind.Buy, Day, InvestedAmount: 100m)));
    }

    [Fact]
    public void Partial_redemption_takes_the_same_share_of_the_invested_amount()
    {
        var position = OpenedApplication(invested: 1000m);

        // Worth 1200 now; redeeming 300 gross is a quarter of it.
        var movement = Ok(position, new MovementInput(PositionMovementKind.Redemption, Day, GrossAmount: 300m, TaxWithheld: 9m, CurrentValue: 1200m));

        Assert.Equal(750m, position.InvestedAmount);
        Assert.Equal(50m, movement.RealizedResult);
        Assert.Equal(291m, movement.NetAmount);
        Assert.True(position.IsOpen);
    }

    [Fact]
    public void Redemption_needs_the_current_value_and_redeeming_it_all_closes()
    {
        var position = OpenedApplication(invested: 1000m);

        Assert.Equal("Positions.CurrentValueRequired",
            Fails(position, new MovementInput(PositionMovementKind.Redemption, Day, GrossAmount: 300m)));

        var movement = Ok(position, new MovementInput(PositionMovementKind.Redemption, Day, GrossAmount: 1250m, CurrentValue: 1200m));

        Assert.Equal(0m, position.InvestedAmount);
        Assert.Equal(250m, movement.RealizedResult);
        Assert.False(position.IsOpen);
    }

    [Fact]
    public void Maturity_redeems_everything_and_closes()
    {
        var position = OpenedApplication(invested: 1000m);

        var movement = Ok(position, new MovementInput(PositionMovementKind.Maturity, Day, GrossAmount: 1180m, TaxWithheld: 31.5m));

        Assert.Equal(0m, position.InvestedAmount);
        Assert.Equal(180m, movement.RealizedResult);
        Assert.Equal(1148.5m, movement.NetAmount);
        Assert.False(position.IsOpen);
    }

    [Fact]
    public void Unit_kinds_do_not_apply_to_an_application_and_vice_versa()
    {
        Assert.Equal("Positions.KindNotAllowed",
            Fails(OpenedApplication(), new MovementInput(PositionMovementKind.Sell, Day, Quantity: 1m, UnitPrice: 1m)));
        Assert.Equal("Positions.KindNotAllowed",
            Fails(OpenedUnit(), new MovementInput(PositionMovementKind.Maturity, Day, GrossAmount: 1m)));
    }

    [Fact]
    public void Costs_and_tax_cannot_exceed_the_gross()
    {
        Assert.Equal("Positions.NegativeNet",
            Fails(OpenedUnit(), new MovementInput(PositionMovementKind.Sell, Day, Quantity: 1m, UnitPrice: 1m, Costs: 2m)));
        Assert.Equal("Positions.InvalidAmount",
            Fails(OpenedUnit(), new MovementInput(PositionMovementKind.Buy, Day, Quantity: 1m, UnitPrice: 1m, Costs: -1m)));
    }

    [Fact]
    public void System_asset_class_is_read_only()
    {
        var own = AssetClass.Create(Guid.NewGuid(), " Crowdfunding ", AssetBehavior.Application, Time);

        Assert.Equal("Crowdfunding", own.Name);
        Assert.True(own.Rename("Equity crowdfunding"));
        Assert.True(own.Archive(Time));
        Assert.False(own.Rename("Again"));
    }

    [Fact]
    public void Asset_codes_are_upper_case_and_trimmed()
    {
        var asset = Asset.Create(Guid.NewGuid(), Guid.NewGuid(), "Itaúsa", " itsa4 ", null, "  ", CurrencyCode.Create("BRL"), Time);

        Assert.Equal("ITSA4", asset.Code);
        Assert.Null(asset.Issuer);
    }
}
