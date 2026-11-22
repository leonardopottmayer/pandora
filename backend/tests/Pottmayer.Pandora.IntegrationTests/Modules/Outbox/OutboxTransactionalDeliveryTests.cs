using System.Net.Http.Json;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Support;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Outbox;

/// <summary>
/// End-to-end proof of the transactional in-process outbox on the real sign-up flow: the event is
/// written in the producer's own transaction, it is not delivered until the relay drains it,
/// and delivery is at-least-once-but-effectively-once thanks to idempotent handlers.
/// </summary>
[Collection("Integration")]
public sealed class OutboxTransactionalDeliveryTests : IAsyncLifetime
{
    private const string SignUpUrl = "/api/v1/identity/auth/signup";
    private const string ActivationEventType = "account-activation-requested";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ChannelsProbe _notifications;

    public OutboxTransactionalDeliveryTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _notifications = new ChannelsProbe(factory.ConnectionString);
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SignUp_writes_the_event_to_the_outbox_in_its_own_transaction_and_defers_delivery()
    {
        var email = "outbox-alice@example.com";
        await SignUpAsync(email, "outboxalice");

        // Before any relay runs, the event is already durably parked in Identity's database — proof the
        // write joined the sign-up transaction rather than being a second, losable step.
        Assert.Equal(1, await CountOutboxAsync(ActivationEventType, status: 0));

        // And nothing has been delivered: no activation notification exists yet.
        Assert.Null(await _notifications.FindByRecipientAsync(email));
    }

    [Fact]
    public async Task Draining_the_outbox_delivers_the_event_and_marks_it_dispatched()
    {
        var email = "outbox-bob@example.com";
        await SignUpAsync(email, "outboxbob");

        await _factory.DrainOutboxAsync();

        // The Channels consumer ran and enqueued the activation e-mail.
        var notification = await _notifications.FindByRecipientAsync(email);
        Assert.NotNull(notification);
        Assert.Equal("account-activation", notification!.TemplateKey);

        // The row is now dispatched, no longer pending.
        Assert.Equal(0, await CountOutboxAsync(ActivationEventType, status: 0));
        Assert.Equal(1, await CountOutboxAsync(ActivationEventType, status: 1));
    }

    [Fact]
    public async Task Draining_twice_delivers_once()
    {
        var email = "outbox-carol@example.com";
        await SignUpAsync(email, "outboxcarol");

        await _factory.DrainOutboxAsync();
        await _factory.DrainOutboxAsync();

        // A dispatched row is never redelivered, so exactly one notification exists.
        Assert.Equal(1, await _notifications.CountAsync());
    }

    private Task<HttpResponseMessage> SignUpAsync(string email, string username)
        => _client.PostAsJsonAsync(SignUpUrl, new
        {
            name = "Outbox User",
            username,
            email,
            password = IdentityHelper.DefaultPassword
        });

    private async Task<int> CountOutboxAsync(string eventType, int status)
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT count(*) FROM identity.tars_outbox_message WHERE event_type = $1 AND status = $2";
        cmd.Parameters.AddWithValue(eventType);
        cmd.Parameters.AddWithValue((short)status);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}
