using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Modules.Integrations;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailbox;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Integrations.Contracts;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Communications;

/// <summary>The sync engine end to end against a scripted provider (registered under <c>titan</c>).</summary>
[Collection("Integration")]
public sealed class SyncEngineTests : IAsyncLifetime
{
    private const string MessageReceived = "communications.mail-message-received";
    private const string NotifyUser = "notify-user-requested";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly FakeMailProvider _provider = new();
    private readonly WebApplicationFactory<Program> _host;

    public SyncEngineTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        // Batches of 2, so a mailbox of a few messages already takes several of them.
        _host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s
            .AddKeyedSingleton<IMailboxProvider>(MailProvider.Titan.Value, _provider)
            .Configure<CommunicationsOptions>(o => o.BackfillBatchSize = 2)));
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task The_backfill_mirrors_the_mailbox_without_events()
    {
        _provider.Deliver("m1", "First", sentAt: DateTimeOffset.UtcNow.AddDays(-2));
        _provider.Deliver("m2", "Second", sentAt: DateTimeOffset.UtcNow.AddDays(-1));
        var mailbox = await CreateMailboxAsync();

        Assert.True(Value(await SyncAsync(mailbox.Id)));

        var synced = await LoadAsync(mailbox.Id);
        Assert.Equal(MailboxStatus.Active, synced.Status);
        Assert.True(synced.IsBackfillComplete);
        Assert.Equal(2, synced.MessageTotalEstimate);
        Assert.Equal(["First", "Second"], (await SubjectsAsync(mailbox.Id)).Order());
        Assert.Equal(0, await CountOutboxAsync(MessageReceived));
    }

    [Fact]
    public async Task A_date_with_the_senders_offset_is_saved_on_arrival_and_on_change()
    {
        var sent = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.FromHours(2));
        _provider.Deliver("m1", "From Berlin", sentAt: sent);
        var mailbox = await CreateMailboxAsync();

        Assert.True(Value(await SyncAsync(mailbox.Id)));

        _provider.Deliver("m1", "From Berlin", sentAt: sent.AddHours(1));

        Assert.True(Value(await SyncAsync(mailbox.Id)));
        Assert.Equal(MailboxStatus.Active, (await LoadAsync(mailbox.Id)).Status);
    }

    [Fact]
    public async Task One_cycle_reads_backfill_batches_until_the_backfill_ends()
    {
        for (var i = 1; i <= 5; i++)
            _provider.Deliver($"m{i}", $"Message {i}", sentAt: DateTimeOffset.UtcNow.AddDays(-i));
        var mailbox = await CreateMailboxAsync();

        await SyncAsync(mailbox.Id);

        Assert.True((await LoadAsync(mailbox.Id)).IsBackfillComplete);
        Assert.Equal(5, (await SubjectsAsync(mailbox.Id)).Count);
    }

    [Fact]
    public async Task An_arrival_after_the_backfill_publishes_one_event()
    {
        _provider.Deliver("m1", "Old");
        var mailbox = await CreateMailboxAsync();
        await SyncAsync(mailbox.Id);

        _provider.Deliver("m2", "New");
        await SyncAsync(mailbox.Id);
        await SyncAsync(mailbox.Id);

        Assert.Equal(["New", "Old"], (await SubjectsAsync(mailbox.Id)).Order());
        Assert.Equal(1, await CountOutboxAsync(MessageReceived));
        Assert.Contains("\"Subject\":\"New\"", await OutboxPayloadAsync(MessageReceived), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Changes_update_flags_and_removals_delete_rows()
    {
        _provider.Deliver("m1", "Keep");
        _provider.Deliver("m2", "Drop");
        var mailbox = await CreateMailboxAsync();
        await SyncAsync(mailbox.Id);

        _provider.MarkRead("m1");
        _provider.Delete("m2");
        await SyncAsync(mailbox.Id);

        Assert.Equal(["Keep"], await SubjectsAsync(mailbox.Id));
        Assert.False(await IsUnreadAsync(mailbox.Id, "m1"));
    }

    [Fact]
    public async Task A_message_missing_from_its_label_snapshot_is_removed()
    {
        _provider.SnapshotLabel = "INBOX";
        _provider.Deliver("m1", "Stays");
        _provider.Deliver("m2", "Moved away");
        var mailbox = await CreateMailboxAsync();
        await SyncAsync(mailbox.Id);

        _provider.Delete("m2");
        await SyncAsync(mailbox.Id);

        Assert.Equal(["Stays"], await SubjectsAsync(mailbox.Id));
    }

    [Fact]
    public async Task A_message_id_the_mailbox_already_has_is_not_an_arrival()
    {
        _provider.Deliver("m1", "Original", rfc822: "same@mail.test");
        var mailbox = await CreateMailboxAsync();
        await SyncAsync(mailbox.Id);

        // A copy or a move: a new provider id for a message the mailbox has.
        _provider.Deliver("m1-copy", "Original", rfc822: "same@mail.test");
        await SyncAsync(mailbox.Id);

        Assert.Equal(2, (await SubjectsAsync(mailbox.Id)).Count);
        Assert.Equal(0, await CountOutboxAsync(MessageReceived));
    }

    [Fact]
    public async Task An_expired_cursor_reads_the_mailbox_again_without_events()
    {
        _provider.Deliver("m1", "First");
        var mailbox = await CreateMailboxAsync();
        await SyncAsync(mailbox.Id);

        _provider.Delete("m1");
        _provider.Deliver("m2", "Second");
        _provider.ExpireCursorOnce = true;
        await SyncAsync(mailbox.Id);

        Assert.Equal(2, _provider.StartBackfillCalls);
        Assert.Equal(["Second"], await SubjectsAsync(mailbox.Id));
        Assert.Equal(0, await CountOutboxAsync(MessageReceived));
    }

    [Fact]
    public async Task A_refused_password_stops_the_mailbox_and_tells_the_user_once()
    {
        var mailbox = await CreateMailboxAsync();
        _provider.FailWith = MailErrors.MailboxAuthFailed("Invalid credentials");

        var first = await SyncAsync(mailbox.Id);
        var second = await SyncAsync(mailbox.Id);

        Assert.Equal("Communications.MailboxAuthFailed", Assert.Single(first.Errors).Code);
        Assert.True(second.IsSuccess);
        Assert.False(second.Value);
        var stopped = await LoadAsync(mailbox.Id);
        Assert.Equal(MailboxStatus.NeedsReconnect, stopped.Status);
        Assert.Contains("Invalid credentials", stopped.LastError);
        Assert.Equal(1, await CountOutboxAsync(NotifyUser));
    }

    [Fact]
    public async Task A_transient_failure_is_counted_and_retried()
    {
        var mailbox = await CreateMailboxAsync();
        _provider.FailWith = MailErrors.ProviderFailed("timeout");

        await SyncAsync(mailbox.Id);

        var failed = await LoadAsync(mailbox.Id);
        Assert.Equal(1, failed.ConsecutiveFailures);
        Assert.True(failed.CanSync);
        Assert.Equal(0, await CountOutboxAsync(NotifyUser));

        _provider.FailWith = null;
        Assert.True(Value(await SyncAsync(mailbox.Id)));
        Assert.Equal(0, (await LoadAsync(mailbox.Id)).ConsecutiveFailures);
    }

    [Fact]
    public async Task A_revoked_account_stops_its_mailboxes()
    {
        var mailbox = await CreateMailboxAsync();

        using (var scope = _host.Services.CreateScope())
        {
            var handlers = scope.ServiceProvider.GetServices<IIntegrationEventHandler<ExternalAccountRevoked>>();
            var ours = handlers.Single(h => h.GetType().Namespace!.StartsWith("Pottmayer.Pandora.Modules.Communications", StringComparison.Ordinal));
            await ours.HandleAsync(new ExternalAccountRevoked(
                Guid.CreateVersion7(), DateTimeOffset.UtcNow, mailbox.UserId, mailbox.ExternalAccountId, "google", "Personal"));
        }

        Assert.Equal(MailboxStatus.NeedsReconnect, (await LoadAsync(mailbox.Id)).Status);
    }

    [Fact]
    public async Task Connections_show_the_mailbox_as_a_usage_of_its_account()
    {
        var client = _host.CreateClient();
        await IdentityHelper.AuthenticateAsync(client, _factory.ConnectionString, "mail-usage@example.com", "mailusage");
        var userId = await AccountsApi.UserIdAsync(_factory.ConnectionString, "mail-usage@example.com");
        var accountId = await AccountsApi.ReadDataAsync<Guid>(await client.PutAsJsonAsync(
            "/api/v1/integrations/titan/password", new { username = "me@titan.test", password = "secret" }));
        await CreateMailboxAsync(userId, accountId, "me@titan.test");

        var account = Assert.Single(await AccountsApi.AccountsAsync(client));

        var usage = Assert.Single(account.Usages);
        Assert.Equal(new AccountsApi.UsageItem("communications", "mail", "me@titan.test"), usage);
    }

    private async Task<Mailbox> CreateMailboxAsync(Guid? userId = null, Guid? accountId = null, string address = "me@titan.test")
    {
        var mailbox = Mailbox.Connect(userId ?? Guid.CreateVersion7(), MailProvider.Titan, accountId ?? Guid.CreateVersion7(), address, null);
        await RunAsync((context, token) => context.AcquireRepository<IMailboxRepository>().AddAsync(mailbox, token));
        return mailbox;
    }

    private async Task<Result<bool>> SyncAsync(Guid mailboxId)
    {
        using var scope = _host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SyncMailboxCommand(new SyncMailboxInput(mailboxId)));
    }

    private async Task<Mailbox> LoadAsync(Guid mailboxId)
    {
        Mailbox? mailbox = null;
        await RunAsync(async (context, token) =>
            mailbox = await context.AcquireRepository<IMailboxRepository>().GetByIdAsync(mailboxId, token));
        return mailbox!;
    }

    private async Task RunAsync(Func<IDataContext, CancellationToken, Task> work)
    {
        using var scope = _host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>().ExecuteAsync(CommunicationsModule.DatabaseKey, work);
    }

    private async Task<List<string>> SubjectsAsync(Guid mailboxId) =>
        await QueryAsync("SELECT subject FROM communications.com003_mail_message WHERE mailbox_id = $1", mailboxId,
            r => r.GetString(0));

    private async Task<bool> IsUnreadAsync(Guid mailboxId, string providerMessageId) =>
        (await QueryAsync($"SELECT is_unread FROM communications.com003_mail_message WHERE mailbox_id = $1 AND provider_message_id = '{providerMessageId}'",
            mailboxId, r => r.GetBoolean(0))).Single();

    private async Task<int> CountOutboxAsync(string eventType) =>
        (await QueryAsync("SELECT count(*)::int FROM communications.tars_outbox_message WHERE event_type = $1", eventType,
            r => r.GetInt32(0))).Single();

    private async Task<string> OutboxPayloadAsync(string eventType) =>
        (await QueryAsync("SELECT payload FROM communications.tars_outbox_message WHERE event_type = $1", eventType,
            r => r.GetString(0))).Single();

    private async Task<List<T>> QueryAsync<T>(string sql, object parameter, Func<NpgsqlDataReader, T> read)
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue(parameter);
        await using var reader = await cmd.ExecuteReaderAsync();
        var rows = new List<T>();
        while (await reader.ReadAsync())
            rows.Add(read(reader));
        return rows;
    }

    private static T Value<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
        return result.Value;
    }
}
