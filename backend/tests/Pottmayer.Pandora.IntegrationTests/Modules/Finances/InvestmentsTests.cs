using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Finances;

/// <summary>
/// The portfolio core over HTTP: positions and their movements, the ledger entry each movement creates
/// (and voids on undo), the guard on those entries, values and totals, and the account balance.
/// </summary>
[Collection("Integration")]
public sealed class InvestmentsTests : IAsyncLifetime
{
    private const string Base = "/api/v1/finances";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public InvestmentsTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Buy_sell_and_undo_keep_position_and_cash_in_step()
    {
        await AuthAsync("inv1");
        var account = await PostAsync("/accounts", new { name = "XP", type = "investment", currency = "BRL", displayOrder = 0, openingBalance = 10000 });
        var asset = await CreateAssetAsync("stock", "Itaúsa", "itsa4");

        var position = await PostAsync("/positions", new
        {
            accountId = Id(account), assetId = Id(asset), kind = "buy", occurredOn = "2026-11-02",
            quantity = 100, unitPrice = 10, costs = 5
        });
        Assert.Equal(100m, Dec(position, "quantity"));
        Assert.Equal(10.05m, Dec(position, "averageCost"));

        // 10 000 opening − 1 005 buy.
        var balance = await GetAsync($"/accounts/{Id(account)}/balance");
        Assert.Equal(8995m, Dec(balance, "posted"));
        Assert.Equal(1005m, Dec(balance, "positionsValue")); // no value reported yet: worth its cost
        Assert.Equal(10000m, Dec(balance, "total"));

        var sold = await PostAsync($"/positions/{Id(position)}/movements", new { kind = "sell", occurredOn = "2026-11-10", quantity = 40, unitPrice = 15 });
        Assert.Equal(60m, Dec(sold, "quantity"));
        Assert.Equal(9595m, Dec(await GetAsync($"/accounts/{Id(account)}/balance"), "posted"));

        var detail = await GetAsync($"/positions/{Id(position)}");
        var movements = detail["movements"]!.AsArray();
        Assert.Equal(["buy", "sell"], movements.Select(m => m!["kind"]!.GetValue<string>()));
        Assert.Equal(198m, Dec(movements[1]!, "realizedResult")); // 600 − 40 × 10.05
        var saleTx = movements[1]!["transactionId"]!.GetValue<Guid>();

        // The sale's ledger entry changes only through the position.
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"{Base}/transactions/{saleTx}/void", null)).StatusCode);

        var undo = await _client.DeleteAsync($"{Base}/positions/{Id(position)}/movements/latest");
        Assert.Equal(HttpStatusCode.OK, undo.StatusCode);
        Assert.Equal(8995m, Dec(await GetAsync($"/accounts/{Id(account)}/balance"), "posted"));
        Assert.Equal(100m, Dec((await GetAsync($"/positions/{Id(position)}"))["position"]!, "quantity"));

        // The voided sale cannot come back on its own: its movement is gone.
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"{Base}/transactions/{saleTx}/unvoid", null)).StatusCode);

        // Undoing the only movement left deletes the position and returns the cash.
        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync($"{Base}/positions/{Id(position)}/movements/latest")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"{Base}/positions/{Id(position)}")).StatusCode);
        Assert.Equal(10000m, Dec(await GetAsync($"/accounts/{Id(account)}/balance"), "posted"));
    }

    [Fact]
    public async Task Fixed_income_from_a_checking_account_with_values_and_summary()
    {
        await AuthAsync("inv2");
        // INV-8: a checking account holds the CDB directly.
        var account = await PostAsync("/accounts", new { name = "Inter", type = "checking", currency = "BRL", displayOrder = 0, openingBalance = 6000 });
        var asset = await CreateAssetAsync("cdb", "CDB Inter 110%", null);

        var cdb = await PostAsync("/positions", new
        {
            accountId = Id(account), assetId = Id(asset), kind = "buy", occurredOn = "2026-10-01",
            investedAmount = 5000, index = "cdi", indexPercent = 110, maturityOn = "2099-01-02"
        });
        Assert.Equal(5000m, Dec(cdb, "currentValue")); // first value = what was invested
        Assert.Equal(1000m, Dec(await GetAsync($"/accounts/{Id(account)}/balance"), "posted"));

        var values = await _client.PostAsJsonAsync($"{Base}/positions/valuations",
            new { valuedOn = "2026-11-20", items = new[] { new { positionId = Id(cdb), value = 5100 } } });
        Assert.Equal(HttpStatusCode.OK, values.StatusCode);

        var balance = await GetAsync($"/accounts/{Id(account)}/balance");
        Assert.Equal(5100m, Dec(balance, "positionsValue"));
        Assert.Equal(6100m, Dec(balance, "total"));

        // Redeeming 2 550 of 5 100 is half: half of the 5 000 invested.
        var redeemed = await PostAsync($"/positions/{Id(cdb)}/movements", new { kind = "redemption", occurredOn = "2026-11-21", grossAmount = 2550, taxWithheld = 10 });
        Assert.Equal(2500m, Dec(redeemed, "investedAmount"));
        // The 5 100 reported before the redemption is moved by it: 2 550 are left (like an account's balance, D1).
        Assert.Equal(2550m, Dec(redeemed, "currentValue"));
        Assert.Equal(2550m, Dec(await GetAsync($"/accounts/{Id(account)}/balance"), "positionsValue"));
        Assert.Equal(3540m, Dec(await GetAsync($"/accounts/{Id(account)}/balance"), "posted"));

        var summary = await GetAsync("/investments/summary");
        var total = Assert.Single(summary["totals"]!.AsArray());
        Assert.Equal("BRL", total!["currency"]!.GetValue<string>());
        Assert.Equal(Id(cdb), summary["upcomingMaturities"]!.AsArray().Single()!["id"]!.GetValue<Guid>());

        // An account with positions keeps its history: it can be archived, not deleted.
        Assert.Equal(HttpStatusCode.Conflict, (await _client.DeleteAsync($"{Base}/accounts/{Id(account)}")).StatusCode);
    }

    [Fact]
    public async Task Opening_moves_no_cash_and_rules_are_enforced()
    {
        await AuthAsync("inv3");
        var brl = await PostAsync("/accounts", new { name = "Broker", type = "investment", currency = "BRL", displayOrder = 0 });
        var usd = await PostAsync("/accounts", new { name = "Avenue", type = "investment", currency = "USD", displayOrder = 0 });
        var asset = await CreateAssetAsync("fii", "HGLG11", "hglg11");

        var opened = await PostAsync("/positions", new
        {
            accountId = Id(brl), assetId = Id(asset), kind = "opening", occurredOn = "2026-11-01",
            quantity = 10, averageCost = 160, currentValue = 1650
        });
        Assert.Equal(1650m, Dec(opened, "currentValue"));
        Assert.Equal(0m, Dec(await GetAsync($"/accounts/{Id(brl)}/balance"), "posted"));

        // One open position per asset and account.
        var again = await _client.PostAsJsonAsync($"{Base}/positions", new
        {
            accountId = Id(brl), assetId = Id(asset), kind = "opening", occurredOn = "2026-11-01", quantity = 1, averageCost = 1
        });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        // The asset's currency must be the account's.
        var mismatch = await _client.PostAsJsonAsync($"{Base}/positions", new
        {
            accountId = Id(usd), assetId = Id(asset), kind = "opening", occurredOn = "2026-11-01", quantity = 1, averageCost = 1
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, mismatch.StatusCode);

        // A rent income lands in the cash, net of tax.
        await PostAsync($"/positions/{Id(opened)}/movements", new { kind = "income", occurredOn = "2026-11-15", grossAmount = 11, incomeKind = "rent" });
        Assert.Equal(11m, Dec(await GetAsync($"/accounts/{Id(brl)}/balance"), "posted"));

        // Selling more than held is refused.
        var oversell = await _client.PostAsJsonAsync($"{Base}/positions/{Id(opened)}/movements",
            new { kind = "sell", occurredOn = "2026-11-16", quantity = 11, unitPrice = 170 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, oversell.StatusCode);
    }

    [Fact]
    public async Task Own_classes_sit_next_to_the_read_only_system_ones()
    {
        await AuthAsync("inv4");

        var own = await PostAsync("/asset-classes", new { name = "Crowdfunding", behavior = "application" });
        var classes = (await _client.GetFromJsonAsync<JsonObject>($"{Base}/asset-classes"))!["data"]!.AsArray();
        Assert.Equal(19, classes.Count);

        var system = classes.First(c => c!["code"]?.GetValue<string>() == "stock")!;
        var renameSystem = await _client.PutAsJsonAsync($"{Base}/asset-classes/{Id(system)}", new { name = "Shares" });
        Assert.Equal(HttpStatusCode.Conflict, renameSystem.StatusCode);

        var renamed = await _client.PutAsJsonAsync($"{Base}/asset-classes/{Id(own)}", new { name = "Equity crowdfunding" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
    }

    [Fact]
    public async Task An_archived_asset_opens_no_position_until_brought_back()
    {
        await AuthAsync("inv5");
        var account = await PostAsync("/accounts", new { name = "Broker", type = "investment", currency = "BRL", displayOrder = 0 });
        var asset = await CreateAssetAsync("cdb", "Itaú Hunter CDB 110%", null);
        var opening = new { accountId = Id(account), assetId = Id(asset), kind = "opening", occurredOn = "2026-11-01", investedAmount = 1000, index = "cdi", indexPercent = 110, maturityOn = "2099-01-02" };

        var archived = await PostAsync($"/assets/{Id(asset)}/archive", new { });
        Assert.NotNull(archived["archivedAt"]);
        var refused = await _client.PostAsJsonAsync($"{Base}/positions", opening);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        var back = await PostAsync($"/assets/{Id(asset)}/unarchive", new { });
        Assert.Null(back["archivedAt"]);
        await PostAsync("/positions", opening);

        // With a position it is history: archive, not delete. One never used goes.
        Assert.Equal(HttpStatusCode.Conflict, (await _client.DeleteAsync($"{Base}/assets/{Id(asset)}")).StatusCode);
        var unused = await CreateAssetAsync("cdb", "Registered by mistake", null);
        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync($"{Base}/assets/{Id(unused)}")).StatusCode);
    }

    [Fact]
    public async Task An_asset_moves_to_a_class_of_the_same_behavior_and_an_unused_own_class_is_deleted()
    {
        await AuthAsync("inv6");
        var own = await PostAsync("/asset-classes", new { name = "Cotas", behavior = "application" });
        var asset = await PostAsync("/assets", new { assetClassId = Id(own), name = "Capital Viacredi", currency = "BRL" });
        var classes = (await _client.GetFromJsonAsync<JsonObject>($"{Base}/asset-classes"))!["data"]!.AsArray();
        Guid ClassId(string code) => classes.First(c => c!["code"]?.GetValue<string>() == code)!["id"]!.GetValue<Guid>();

        var inUse = await _client.DeleteAsync($"{Base}/asset-classes/{Id(own)}");
        Assert.Equal(HttpStatusCode.Conflict, inUse.StatusCode);

        var toUnit = await _client.PutAsJsonAsync($"{Base}/assets/{Id(asset)}", new { name = "Capital Viacredi", assetClassId = ClassId("stock") });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, toUnit.StatusCode);

        var moved = await _client.PutAsJsonAsync($"{Base}/assets/{Id(asset)}", new { name = "Capital Viacredi", assetClassId = ClassId("capital-quotas") });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(ClassId("capital-quotas"), (await moved.Content.ReadFromJsonAsync<JsonObject>())!["data"]!["assetClassId"]!.GetValue<Guid>());

        // A buy into an application is a contribution, and its ledger entry says so.
        var account = await PostAsync("/accounts", new { name = "Viacredi", type = "checking", currency = "BRL", displayOrder = 0, openingBalance = 100 });
        var position = await PostAsync("/positions", new { accountId = Id(account), assetId = Id(asset), kind = "opening", occurredOn = "2026-11-26", investedAmount = 1490 });
        await PostAsync($"/positions/{Id(position)}/movements", new { kind = "buy", occurredOn = "2026-11-27", grossAmount = 50 });
        var entries = (await _client.GetFromJsonAsync<JsonObject>($"{Base}/transactions?accountId={Id(account)}"))!["data"]!.AsArray();
        Assert.Contains(entries, e => e!["descriptionKey"]?.GetValue<string>() == "transaction.investment-contribution");

        var deleted = await _client.DeleteAsync($"{Base}/asset-classes/{Id(own)}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        var system = await _client.DeleteAsync($"{Base}/asset-classes/{ClassId("savings")}");
        Assert.Equal(HttpStatusCode.Conflict, system.StatusCode);
    }

    private Task AuthAsync(string username) =>
        IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, $"{username}@example.com", username);

    private async Task<JsonObject> CreateAssetAsync(string classCode, string name, string? code)
    {
        var classes = (await _client.GetFromJsonAsync<JsonObject>($"{Base}/asset-classes"))!["data"]!.AsArray();
        var classId = classes.First(c => c!["code"]?.GetValue<string>() == classCode)!["id"]!.GetValue<Guid>();
        return await PostAsync("/assets", new { assetClassId = classId, name, code, currency = "BRL" });
    }

    private async Task<JsonObject> PostAsync(string path, object body)
    {
        var response = await _client.PostAsJsonAsync(Base + path, body);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {(int)response.StatusCode} {json}");
        return json!["data"]!.AsObject();
    }

    private async Task<JsonObject> GetAsync(string path)
        => (await _client.GetFromJsonAsync<JsonObject>(Base + path))!["data"]!.AsObject();

    private static Guid Id(JsonNode node) => node["id"]!.GetValue<Guid>();

    private static decimal Dec(JsonNode node, string field) => node[field]!.GetValue<decimal>();
}
