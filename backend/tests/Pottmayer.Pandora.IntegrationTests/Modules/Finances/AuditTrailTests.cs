using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Finances.Abstractions;
using Pottmayer.Pandora.Modules.Finances.Application.Auditing;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Finances;

/// <summary>
/// Validates the audit trail end to end: an event written through <c>IAuditEventRepository</c> (in the same
/// unit of work) surfaces on <c>GET /finances/audit</c>, scoped to the owning user.
/// </summary>
[Collection("Integration")]
public sealed class AuditTrailTests : IAsyncLifetime
{
    private const string AuditUrl = "/api/v1/finances/audit";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuditTrailTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Get_returns_unauthorized_without_a_token()
    {
        var response = await _client.GetAsync($"{AuditUrl}?correlationId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_requires_a_filter()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "noah@example.com", "noah");

        var response = await _client.GetAsync(AuditUrl);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Recorded_event_appears_in_the_entity_timeline()
    {
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "olivia@example.com", "olivia");
        var userId = await GetUserIdAsync("olivia@example.com");

        var entityId = Guid.CreateVersion7();
        await WriteAuditEventAsync(userId, entityId, "transaction", "transaction.created",
            new { description = new { old = (string?)null, @new = "Rent" } });

        var response = await _client.GetAsync($"{AuditUrl}?entityType=transaction&entityId={entityId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var events = await ReadEventsAsync(response);
        var e = Assert.Single(events);
        Assert.Equal("transaction", e.EntityType);
        Assert.Equal(entityId, e.EntityId);
        Assert.Equal("transaction.created", e.EventType);
        Assert.Equal(userId, e.ActorUserId);
        Assert.Contains("Rent", e.Data);
        Assert.NotEqual(default, e.OccurredAt);
    }

    [Fact]
    public async Task Events_are_scoped_to_the_owner()
    {
        // Owner writes an event...
        await IdentityHelper.RegisterActiveUserAsync(_client, _factory.ConnectionString, "owner@example.com", "owner");
        var ownerId = await GetUserIdAsync("owner@example.com");
        var entityId = Guid.CreateVersion7();
        await WriteAuditEventAsync(ownerId, entityId, "account", "account.created", null);

        // ...a different user must not see it.
        await IdentityHelper.AuthenticateAsync(_client, _factory.ConnectionString, "intruder@example.com", "intruder");
        var response = await _client.GetAsync($"{AuditUrl}?entityType=account&entityId={entityId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await ReadEventsAsync(response));
    }

    private async Task WriteAuditEventAsync(Guid userId, Guid entityId, string entityType, string eventType, object? data)
    {
        using var scope = _factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();
        await uow.ExecuteAsync(FinancesModule.Name, (ctx, ct) =>
            ctx.RecordAsync(userId, userId, entityType, entityId, eventType, DateTimeOffset.UtcNow, data, ct: ct));
    }

    private async Task<Guid> GetUserIdAsync(string email)
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM identity.idt001_user WHERE email = $1";
        cmd.Parameters.AddWithValue(email.ToLowerInvariant());
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<IReadOnlyList<AuditEventData>> ReadEventsAsync(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<Envelope>();
        return envelope!.Data;
    }

    private sealed record Envelope(IReadOnlyList<AuditEventData> Data);
    private sealed record AuditEventData(
        Guid Id, Guid? ActorUserId, string EntityType, Guid EntityId, string EventType,
        string? Data, Guid? CorrelationId, DateTimeOffset OccurredAt);
}
