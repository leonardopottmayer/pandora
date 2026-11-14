using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.RunRecurrenceGeneration;
using Pottmayer.Pandora.Modules.Finances.Application.Commands.RunStatementLifecycle;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Finances;

/// <summary>
/// The daily jobs run for every user in one pass, each on that user's own calendar day: at 16:00 UTC on
/// June 10 a Tokyo user is already on June 11 while a São Paulo user is still on June 10. The zones come
/// from the real Identity preferences.
/// </summary>
[Collection("Integration")]
public sealed class UserDayJobsTests : IAsyncLifetime
{
    private readonly PandoraWebApplicationFactory _factory;
    private readonly WebApplicationFactory<Program> _host;
    private readonly Clock _clock = new() { Now = DateTimeOffset.UtcNow }; // real time while signing in: tokens expire on the real clock

    public UserDayJobsTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(_clock)));
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Statements_close_on_each_owners_own_closing_day()
    {
        var saoPaulo = await UserAsync("days-sp", "America/Sao_Paulo");
        var tokyo = await UserAsync("days-tokyo", "Asia/Tokyo");
        var spCard = await CardAsync(saoPaulo);
        var tokyoCard = await CardAsync(tokyo);

        await RunLifecycleAsync();

        Assert.Equal("open", await StatusAsync(saoPaulo, spCard, "2026-06"));
        Assert.Equal("closed", await StatusAsync(tokyo, tokyoCard, "2026-06"));

        _clock.Now = new DateTimeOffset(2026, 6, 11, 4, 0, 0, TimeSpan.Zero); // 01:00 in São Paulo
        await RunLifecycleAsync();

        Assert.Equal("closed", await StatusAsync(saoPaulo, spCard, "2026-06"));
    }

    [Fact]
    public async Task A_scheduled_transaction_posts_when_its_owners_day_arrives()
    {
        var saoPaulo = await UserAsync("post-sp", "America/Sao_Paulo");
        var tokyo = await UserAsync("post-tokyo", "Asia/Tokyo");
        var june11 = new DateOnly(2026, 6, 11);

        // June 11 is tomorrow in São Paulo (scheduled) and already today in Tokyo (posted on the spot).
        var spTx = await ExpenseAsync(saoPaulo, june11);
        var tokyoTx = await ExpenseAsync(tokyo, june11);
        Assert.Equal("pending", await TxStatusAsync(saoPaulo, spTx));
        Assert.Equal("posted", await TxStatusAsync(tokyo, tokyoTx));

        await RunGenerationAsync();
        Assert.Equal("pending", await TxStatusAsync(saoPaulo, spTx));

        _clock.Now = new DateTimeOffset(2026, 6, 11, 4, 0, 0, TimeSpan.Zero); // 01:00 in São Paulo
        await RunGenerationAsync();
        Assert.Equal("posted", await TxStatusAsync(saoPaulo, spTx));
    }

    // ── helpers ──

    private static readonly DateTimeOffset June10 = new(2026, 6, 10, 16, 0, 0, TimeSpan.Zero);

    private async Task<HttpClient> UserAsync(string username, string timeZone)
    {
        _clock.Now = DateTimeOffset.UtcNow;
        var client = _host.CreateClient();
        await IdentityHelper.AuthenticateAsync(client, _factory.ConnectionString, $"{username}@example.com", username);
        var prefs = await client.PutAsJsonAsync("/api/v1/identity/preferences", new
        {
            theme = "dark", language = "en", timeZone, weekStartsOn = "monday", palette = "default", accentColor = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, prefs.StatusCode);
        _clock.Now = June10;
        return client;
    }

    private static async Task<Guid> AccountAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/finances/accounts",
            new { name = "Checking", type = "checking", currency = "BRL", displayOrder = 0, openingBalance = 1000m });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<Node>>())!.Data.Id;
    }

    private static async Task<Guid> CardAsync(HttpClient client)
    {
        var account = await AccountAsync(client);
        var response = await client.PostAsJsonAsync("/api/v1/finances/cards",
            new { name = "Visa", closingDay = 11, dueDay = 20, currency = "BRL", accountId = account });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var card = (await response.Content.ReadFromJsonAsync<Envelope<Node>>())!.Data.Id;

        var purchase = await client.PostAsJsonAsync("/api/v1/finances/transactions",
            new { cardId = card, kind = "expense", amount = 100m, occurredOn = new DateOnly(2026, 6, 5), description = "Lunch" });
        Assert.Equal(HttpStatusCode.OK, purchase.StatusCode);
        return card;
    }

    private static async Task<Guid> ExpenseAsync(HttpClient client, DateOnly occurredOn)
    {
        var account = await AccountAsync(client);
        var response = await client.PostAsJsonAsync("/api/v1/finances/transactions",
            new { accountId = account, kind = "expense", amount = 10m, occurredOn, description = "Rent" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Envelope<Node>>())!.Data.Id;
    }

    private static async Task<string> StatusAsync(HttpClient client, Guid card, string referenceMonth)
    {
        var statements = (await client.GetFromJsonAsync<Envelope<List<StatementNode>>>($"/api/v1/finances/cards/{card}/statements"))!.Data;
        return Assert.Single(statements, s => s.ReferenceMonth == referenceMonth).Status;
    }

    private static async Task<string> TxStatusAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<Envelope<TxNode>>($"/api/v1/finances/transactions/{id}"))!.Data.Status;

    private async Task RunLifecycleAsync()
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new RunStatementLifecycleCommand(new RunStatementLifecycleInput()));
        Assert.True(result.IsSuccess);
    }

    private async Task RunGenerationAsync()
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new RunRecurrenceGenerationCommand(new RunRecurrenceGenerationInput()));
        Assert.True(result.IsSuccess);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed record Envelope<T>(T Data);
    private sealed record Node(Guid Id);
    private sealed record TxNode(Guid Id, string Status);
    private sealed record StatementNode(Guid Id, string ReferenceMonth, string Status);
}
