using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailbox;
using Pottmayer.Pandora.Modules.Communications.Contracts;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Pottmayer.Tars.Messaging.Abstractions;
using Pottmayer.Tars.Messaging.EntityFrameworkCore.Outbox;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Communications;

/// <summary>Rules on a mailbox of folders: a real cycle publishes the arrival, and the handler gets what the outbox holds.</summary>
[Collection("Integration")]
public sealed class MailRulesEngineTests : IAsyncLifetime
{
    private readonly PandoraWebApplicationFactory _factory;
    private readonly FakeMailProvider _provider = new() { Capabilities = new MailboxCapabilities(false, false, false) };
    private readonly WebApplicationFactory<Program> _host;

    public MailRulesEngineTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddKeyedSingleton<IMailboxProvider>(MailProvider.Titan.Value, _provider)));

        _provider.Labels.Clear();
        _provider.Labels.AddRange(
        [
            new RemoteLabel("INBOX", "INBOX", LabelKind.System, LabelRole.Inbox, IsExclusive: true),
            new RemoteLabel("Trash", "Trash", LabelKind.System, LabelRole.Trash, IsExclusive: true),
            new RemoteLabel("Work", "Work", LabelKind.User, null, IsExclusive: true),
        ]);
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_new_message_that_matches_is_moved_and_marked_read()
    {
        var mailbox = await ConnectAsync();
        await AddRuleAsync(mailbox, FromAna, [new(RuleActionKinds.MarkRead, null), await MoveToWorkAsync(mailbox)]);

        _provider.Deliver("m1", "New");
        await SyncAsync(mailbox);
        await DeliverArrivalsAsync();

        Assert.Equal(["flags:m1:False:", "move:m1:Work"], _provider.Writes);
        Assert.Equal(["Work"], await LabelsOfMessageAsync("m1"));
    }

    [Fact]
    public async Task Old_mail_is_left_alone()
    {
        _provider.Deliver("old", "Old");
        var mailbox = await ConnectAsync();
        await AddRuleAsync(mailbox, FromAna, [await MoveToWorkAsync(mailbox)]);

        await SyncAsync(mailbox);
        await DeliverArrivalsAsync();

        Assert.Empty(_provider.Writes);
    }

    [Fact]
    public async Task A_message_that_does_not_match_is_left_alone()
    {
        var mailbox = await ConnectAsync();
        await AddRuleAsync(mailbox, [new MailRuleCondition(RuleConditionKinds.From, "bo")], [await MoveToWorkAsync(mailbox)]);

        _provider.Deliver("m1", "New");
        await SyncAsync(mailbox);
        await DeliverArrivalsAsync();

        Assert.Empty(_provider.Writes);
    }

    [Fact]
    public async Task An_event_delivered_twice_acts_once()
    {
        var mailbox = await ConnectAsync();
        await AddRuleAsync(mailbox, FromAna, [new(RuleActionKinds.MarkRead, null), await MoveToWorkAsync(mailbox)]);

        _provider.Deliver("m1", "New");
        await SyncAsync(mailbox);
        await DeliverArrivalsAsync();
        await DeliverArrivalsAsync();

        Assert.Equal(["flags:m1:False:", "move:m1:Work"], _provider.Writes);
    }

    [Fact]
    public async Task A_trash_delivered_twice_acts_once()
    {
        var mailbox = await ConnectAsync();
        await AddRuleAsync(mailbox, FromAna, [new(RuleActionKinds.Trash, null)]);

        _provider.Deliver("m1", "New");
        await SyncAsync(mailbox);
        await DeliverArrivalsAsync();
        await DeliverArrivalsAsync();

        Assert.Equal(["trash:m1"], _provider.Writes);
    }

    [Fact]
    public async Task A_provider_failure_throws_for_the_outbox_keeps_the_error_on_the_rule_and_the_sync_goes_on()
    {
        var mailbox = await ConnectAsync();
        var rule = await AddRuleAsync(mailbox, FromAna, [await MoveToWorkAsync(mailbox)]);
        _provider.Deliver("m1", "New");
        await SyncAsync(mailbox);

        _provider.FailWith = MailErrors.ProviderFailed("Server is down");
        await Assert.ThrowsAsync<InvalidOperationException>(DeliverArrivalsAsync);
        Assert.Equal("Server is down", (await LoadRuleAsync(rule.Id)).LastError);
        _provider.FailWith = null;

        Assert.Equal(MailboxStatus.Active, (await SyncAsync(mailbox)).Status);

        // The outbox retries, and it works now.
        await DeliverArrivalsAsync();
        Assert.Equal(["move:m1:Work"], _provider.Writes);
        Assert.Null((await LoadRuleAsync(rule.Id)).LastError);
    }

    [Fact]
    public async Task A_failure_that_would_only_repeat_stays_on_the_rule_and_is_not_retried()
    {
        var mailbox = await ConnectAsync();
        var rule = await AddRuleAsync(mailbox, FromAna, [new(RuleActionKinds.Archive, null)]);
        _provider.Deliver("m1", "New");
        await SyncAsync(mailbox);

        await DeliverArrivalsAsync();

        Assert.Equal(MailErrors.NoArchiveFolder.Message, (await LoadRuleAsync(rule.Id)).LastError);
        Assert.Empty(_provider.Writes);
    }

    [Fact]
    public async Task A_folder_that_is_gone_breaks_its_rule_and_a_broken_rule_does_not_run()
    {
        var mailbox = await ConnectAsync();
        var move = await MoveToWorkAsync(mailbox);
        var broken = await AddRuleAsync(mailbox, FromAna, [move]);
        var other = await AddRuleAsync(mailbox, [new MailRuleCondition(RuleConditionKinds.Subject, "nothing")], [new(RuleActionKinds.Star, null)]);

        _provider.Labels.RemoveAll(l => l.ProviderLabelId == "Work");
        _provider.Deliver("m1", "New");
        await SyncAsync(mailbox);
        await DeliverArrivalsAsync();

        var saved = await LoadRuleAsync(broken.Id);
        Assert.Equal(RuleStatus.Broken, saved.Status);
        Assert.Contains("Work", saved.LastError);
        Assert.Equal(RuleStatus.Ok, (await LoadRuleAsync(other.Id)).Status);
        Assert.Empty(_provider.Writes);
    }

    [Fact]
    public async Task A_disabled_rule_does_not_run()
    {
        var mailbox = await ConnectAsync();
        var rule = await AddRuleAsync(mailbox, FromAna, [await MoveToWorkAsync(mailbox)]);
        await RunAsync(async (ctx, ct) =>
        {
            var rules = ctx.AcquireRepository<IMailRuleRepository>();
            var tracked = await rules.GetByIdAsync(rule.Id, ct);
            tracked!.SetEnabled(false);
            await rules.UpdateAsync(tracked, ct);
        });

        _provider.Deliver("m1", "New");
        await SyncAsync(mailbox);
        await DeliverArrivalsAsync();

        Assert.Empty(_provider.Writes);
    }

    [Fact]
    public async Task The_body_is_fetched_only_when_a_rule_still_needs_it()
    {
        var mailbox = await ConnectAsync();
        await AddRuleAsync(mailbox, [new MailRuleCondition(RuleConditionKinds.From, "bo"), new MailRuleCondition(RuleConditionKinds.HasWords, "text of")],
            [new(RuleActionKinds.Star, null)]);

        _provider.Deliver("m1", "New");
        await SyncAsync(mailbox);
        await DeliverArrivalsAsync();

        Assert.Equal(0, _provider.RawFetches);
        Assert.Empty(_provider.Writes);
    }

    [Fact]
    public async Task A_rule_on_the_words_of_the_body_runs_on_them()
    {
        var mailbox = await ConnectAsync();
        await AddRuleAsync(mailbox, [new MailRuleCondition(RuleConditionKinds.HasWords, "text of")], [new(RuleActionKinds.Star, null)]);

        _provider.Deliver("m1", "New");
        await SyncAsync(mailbox);
        await DeliverArrivalsAsync();

        Assert.Equal(1, _provider.RawFetches);
        Assert.Equal(["flags:m1::True"], _provider.Writes);
    }

    private static readonly MailRuleCondition[] FromAna = [new(RuleConditionKinds.From, "ana")];

    private async Task<Mailbox> ConnectAsync()
    {
        var mailbox = Mailbox.Connect(Guid.CreateVersion7(), MailProvider.Titan, Guid.CreateVersion7(), "me@titan.test", null);
        await RunAsync((ctx, ct) => ctx.AcquireRepository<IMailboxRepository>().AddAsync(mailbox, ct));

        // The first cycle starts the backfill; old mail is what it reads.
        await SyncAsync(mailbox);
        return mailbox;
    }

    private async Task<MailRuleAction> MoveToWorkAsync(Mailbox mailbox)
    {
        Guid id = default;
        await RunAsync(async (ctx, ct) =>
            id = (await ctx.AcquireRepository<IMailLabelRepository>().ListByMailboxAsync(mailbox.Id, ct)).Single(l => l.Name == "Work").Id);
        return new MailRuleAction(RuleActionKinds.Move, id);
    }

    private async Task<MailRule> AddRuleAsync(Mailbox mailbox, IEnumerable<MailRuleCondition> conditions, IEnumerable<MailRuleAction> actions)
    {
        var rule = MailRule.Create(mailbox.Id, "Rule", 0, conditions, actions, RuleExecution.Pandora);
        await RunAsync((ctx, ct) => ctx.AcquireRepository<IMailRuleRepository>().AddAsync(rule, ct));
        return rule;
    }

    private async Task<MailRule> LoadRuleAsync(Guid id)
    {
        MailRule? rule = null;
        await RunAsync(async (ctx, ct) => rule = await ctx.AcquireRepository<IMailRuleRepository>().GetByIdAsync(id, ct));
        return rule!;
    }

    private async Task<Mailbox> SyncAsync(Mailbox mailbox)
    {
        using (var scope = _host.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SyncMailboxCommand(new SyncMailboxInput(mailbox.Id)));

        Mailbox? saved = null;
        await RunAsync(async (ctx, ct) => saved = await ctx.AcquireRepository<IMailboxRepository>().GetByIdAsync(mailbox.Id, ct));
        return saved!;
    }

    /// <summary>What the relay does with each arrival the outbox holds, whether or not it was delivered before.</summary>
    private async Task DeliverArrivalsAsync()
    {
        foreach (var payload in await QueryAsync<string>(
            "SELECT payload FROM communications.tars_outbox_message WHERE event_type = 'communications.mail-message-received'"))
        {
            using var scope = _host.Services.CreateScope();
            var serializer = scope.ServiceProvider.GetRequiredService<IIntegrationEventSerializer>();
            var @event = (MailMessageReceived)serializer.DeserializePayload(typeof(MailMessageReceived), payload);

            foreach (var handler in scope.ServiceProvider.GetServices<IIntegrationEventHandler<MailMessageReceived>>())
                await handler.HandleAsync(@event);
        }
    }

    private Task<List<string>> LabelsOfMessageAsync(string providerMessageId) =>
        QueryAsync<string>($"""
            SELECT l.name FROM communications.com004_mail_message_label ml
            JOIN communications.com002_mail_label l ON l.id = ml.label_id
            JOIN communications.com003_mail_message m ON m.id = ml.message_id
            WHERE m.provider_message_id = '{providerMessageId}'
            """);

    private async Task RunAsync(Func<IDataContext, CancellationToken, Task> work)
    {
        using var scope = _host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>().ExecuteAsync(CommunicationsModule.DatabaseKey, work);
    }

    private async Task<List<T>> QueryAsync<T>(string sql)
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync();
        var rows = new List<T>();
        while (await reader.ReadAsync())
            rows.Add(reader.GetFieldValue<T>(0));
        return rows;
    }
}
