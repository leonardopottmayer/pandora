using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Finances.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Finances;

/// <summary>The EF mappings against the fin020–fin025 tables: what is written reads back the same.</summary>
[Collection("Integration")]
public sealed class InvestmentsPersistenceTests(PandoraWebApplicationFactory factory) : IAsyncLifetime
{
    private static readonly DateOnly Day = new(2026, 11, 23);

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task System_classes_are_seeded_next_to_the_users_own()
    {
        var userId = Guid.CreateVersion7();
        var own = UserAssetClass.Create(userId, "Crowdfunding", AssetBehavior.Application, TimeProvider.System);

        await RunAsync((ctx, ct) => ctx.AcquireRepository<IUserAssetClassRepository>().AddAsync(own, ct));

        await RunAsync(async (ctx, ct) =>
        {
            var classes = await ctx.AcquireRepository<ISystemAssetClassRepository>().ListAsync(ct);
            var mine = await ctx.AcquireRepository<IUserAssetClassRepository>().ListByUserAsync(userId, ct);

            Assert.Equal(15, classes.Count);
            Assert.Equal(AssetBehavior.Application, classes.Single(c => c.Code == "cdb").Behavior);
            Assert.Equal(AssetBehavior.Unit, classes.Single(c => c.Code == "stock").Behavior);
            Assert.Equal(own.Id, Assert.Single(mine).Id);
        });
    }

    [Fact]
    public async Task Unit_position_movement_and_valuation_round_trip()
    {
        var userId = Guid.CreateVersion7();
        var account = Account.Create(userId, "XP", AccountType.Investment, CurrencyCode.Create("BRL"),
            null, null, null, null, 0, TimeProvider.System);
        Guid stockClassId = default;
        await RunAsync(async (ctx, ct) =>
        {
            await ctx.AcquireRepository<IAccountRepository>().AddAsync(account, ct);
            stockClassId = (await ctx.AcquireRepository<ISystemAssetClassRepository>().ListAsync(ct)).Single(c => c.Code == "stock").Id;
        });

        var asset = Asset.Create(userId, new AssetClassRef(stockClassId, true), "Itaúsa", "itsa4", "BRITSAACNPR7", null, CurrencyCode.Create("BRL"), TimeProvider.System);
        var position = Position.CreateUnit(userId, account.Id, asset.Id, TimeProvider.System);
        var opening = position.Apply(new MovementInput(PositionMovementKind.Opening, Day, Quantity: 100.5m, AverageCost: 9.1234567891m), true, TimeProvider.System).Value!;
        var income = position.Apply(new MovementInput(PositionMovementKind.Income, Day, GrossAmount: 12.34m, TaxWithheld: 1.85m,
            IncomeKind: IncomeKind.InterestOnEquity, Notes: " JCP "), false, TimeProvider.System).Value!;
        var value = PositionValuation.Create(userId, position.Id, Day, 1050.255m, ValuationSource.Manual, TimeProvider.System);

        await RunAsync(async (ctx, ct) =>
        {
            await ctx.AcquireRepository<IAssetRepository>().AddAsync(asset, ct);
            await ctx.AcquireRepository<IPositionRepository>().AddAsync(position, ct);
            var movements = ctx.AcquireRepository<IPositionMovementRepository>();
            await movements.AddAsync(opening, ct);
            await movements.AddAsync(income, ct);
            await ctx.AcquireRepository<IPositionValuationRepository>().AddAsync(value, ct);
        });

        await RunAsync(async (ctx, ct) =>
        {
            var savedAsset = await ctx.AcquireRepository<IAssetRepository>().FindByIdForUserAsync(asset.Id, userId, ct);
            Assert.Equal("ITSA4", savedAsset!.Code);
            Assert.Equal("BRL", savedAsset.Currency.Value);

            var positions = ctx.AcquireRepository<IPositionRepository>();
            var saved = await positions.FindOpenUnitAsync(account.Id, asset.Id, ct);
            Assert.NotNull(saved);
            Assert.Equal(100.5m, saved.Quantity);
            Assert.Equal(9.1234567891m, saved.AverageCost);
            Assert.True(await positions.AnyForAccountAsync(account.Id, ct));

            var movements = ctx.AcquireRepository<IPositionMovementRepository>();
            var list = await movements.ListByPositionAsync(position.Id, ct);
            Assert.Equal([PositionMovementKind.Opening, PositionMovementKind.Income], list.Select(m => m.Kind));
            var latest = await movements.FindLatestAsync(position.Id, ct);
            Assert.Equal(income.Id, latest!.Id);
            Assert.Equal(IncomeKind.InterestOnEquity, latest.IncomeKind);
            Assert.Equal(10.49m, latest.NetAmount);
            Assert.Equal("JCP", latest.Notes);
            Assert.Equal(100.5m, latest.QuantityBefore);

            var latestValues = await ctx.AcquireRepository<IPositionValuationRepository>().GetLatestAsync([position.Id], ct);
            Assert.Equal(1050.26m, latestValues[position.Id].Value);
        });
    }

    [Fact]
    public async Task Application_position_keeps_its_terms()
    {
        var userId = Guid.CreateVersion7();
        var account = Account.Create(userId, "Inter", AccountType.Checking, CurrencyCode.Create("BRL"),
            null, null, null, null, 0, TimeProvider.System);
        var ownClass = UserAssetClass.Create(userId, "Bank bond", AssetBehavior.Application, TimeProvider.System);
        var asset = Asset.Create(userId, new AssetClassRef(ownClass.Id, false), "CDB Inter 110%", null, null, "Banco Inter", CurrencyCode.Create("BRL"), TimeProvider.System);
        var position = Position.CreateApplication(userId, account.Id, asset.Id, Day, FixedIncomeIndex.Ipca, null, 6.5m,
            new DateOnly(2029, 5, 15), TimeProvider.System);
        var opening = position.Apply(new MovementInput(PositionMovementKind.Opening, Day, InvestedAmount: 5000m), true, TimeProvider.System).Value!;

        await RunAsync(async (ctx, ct) =>
        {
            await ctx.AcquireRepository<IAccountRepository>().AddAsync(account, ct);
            await ctx.AcquireRepository<IUserAssetClassRepository>().AddAsync(ownClass, ct);
            await ctx.AcquireRepository<IAssetRepository>().AddAsync(asset, ct);
            await ctx.AcquireRepository<IPositionRepository>().AddAsync(position, ct);
            await ctx.AcquireRepository<IPositionMovementRepository>().AddAsync(opening, ct);
        });

        await RunAsync(async (ctx, ct) =>
        {
            var saved = Assert.Single(await ctx.AcquireRepository<IPositionRepository>().ListAsync(userId, account.Id, openOnly: true, ct));
            Assert.Equal(AssetBehavior.Application, saved.Behavior);
            Assert.Equal(5000m, saved.InvestedAmount);
            Assert.Equal(FixedIncomeIndex.Ipca, saved.Index);
            Assert.Equal(6.5m, saved.SpreadRate);
            Assert.Null(saved.IndexPercent);
            Assert.Equal(new DateOnly(2029, 5, 15), saved.MaturityOn);
            Assert.Null(saved.Quantity);
        });
    }

    private async Task RunAsync(Func<IDataContext, CancellationToken, Task> work)
    {
        using var scope = factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();
        await uow.ExecuteAsync(FinancesModule.DatabaseKey, work);
    }
}
