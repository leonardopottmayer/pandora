using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Communications.Abstractions;
using Pottmayer.Pandora.Modules.Communications.Application.Commands.SyncMailbox;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Data.Abstractions.DataContext;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Communications;

/// <summary>Gmail's filters reaching the rules through real cycles.</summary>
[Collection("Integration")]
public sealed class FilterSyncTests : IAsyncLifetime
{
    private readonly PandoraWebApplicationFactory _factory;
    private readonly FakeMailProvider _provider = new() { Capabilities = new MailboxCapabilities(true, true, true) };
    private readonly WebApplicationFactory<Program> _host;

    public FilterSyncTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddKeyedSingleton<IMailboxProvider>(MailProvider.Gmail.Value, _provider)));

        _provider.Labels.Add(new RemoteLabel("Label_1", "Work", LabelKind.User, null, IsExclusive: false));
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Filters_already_there_on_connect_become_rules_and_a_later_cycle_adds_none()
    {
        _provider.Filters.Add(Star("f1"));
        _provider.Filters.Add(new RemoteFilter("f2", "from: bo", [new(RuleConditionKinds.From, "bo")],
            [new(RuleActionKinds.Move, "Label_1")], "{}", IsReadOnly: false));

        var mailbox = await ConnectAsync();
        await SyncAsync(mailbox);

        var rules = await RulesAsync(mailbox);
        Assert.Equal(["f1", "f2"], rules.Select(r => r.ProviderFilterId));
        Assert.Equal([0, 1], rules.Select(r => r.Position));

        var moved = rules[1];
        Assert.Equal("from: bo", moved.Name);
        Assert.Equal(RuleExecution.ProviderFilter, moved.Execution);
        Assert.Equal(RuleStatus.Ok, moved.Status);
        Assert.Equal([new MailRuleCondition(RuleConditionKinds.From, "bo")], moved.Conditions);
        Assert.Equal(await LabelIdAsync(mailbox, "Work"), Assert.Single(moved.Actions).LabelId);
        Assert.False(moved.RunsInPandora);
    }

    [Fact]
    public async Task A_filter_that_does_not_fit_is_read_only_with_what_gmail_sent()
    {
        _provider.Filters.Add(new RemoteFilter("f1", "size: 100", [], [], """{"id": "f1", "action": {"forward": "x@mail.test"}}""", IsReadOnly: true));

        var rule = Assert.Single(await RulesAsync(await ConnectAsync()));

        Assert.Equal(RuleStatus.ReadOnly, rule.Status);
        Assert.Equal("size: 100", rule.Name);
        Assert.Contains("forward", rule.ProviderFilterRaw);
    }

    [Fact]
    public async Task A_filter_on_a_label_not_known_here_is_read_only_not_dropped()
    {
        _provider.Filters.Add(new RemoteFilter("f1", "from: ana", [new(RuleConditionKinds.From, "ana")],
            [new(RuleActionKinds.AddLabel, "Label_404")], "{}", IsReadOnly: false));

        var rule = Assert.Single(await RulesAsync(await ConnectAsync()));

        Assert.Equal(RuleStatus.ReadOnly, rule.Status);
        Assert.Equal("f1", rule.ProviderFilterId);
    }

    [Fact]
    public async Task A_filter_that_is_gone_takes_its_rule_but_not_a_disabled_one()
    {
        _provider.Filters.Add(Star("f1"));
        _provider.Filters.Add(Star("f2"));
        var mailbox = await ConnectAsync();

        var disabled = (await RulesAsync(mailbox)).Single(r => r.ProviderFilterId == "f2");
        await RunAsync(async (ctx, ct) =>
        {
            var rules = ctx.AcquireRepository<IMailRuleRepository>();
            var tracked = await rules.GetByIdAsync(disabled.Id, ct);
            tracked!.SetEnabled(false);
            tracked.BindFilter(null);
            await rules.UpdateAsync(tracked, ct);
        });

        _provider.Filters.Clear();
        await SyncAsync(mailbox);

        Assert.Equal(disabled.Id, Assert.Single(await RulesAsync(mailbox)).Id);
    }

    [Fact]
    public async Task One_gone_and_one_new_is_an_edit_keeping_the_rule_its_name_and_its_position()
    {
        _provider.Filters.Add(Star("f0"));
        _provider.Filters.Add(Star("f1"));
        var mailbox = await ConnectAsync();
        var edited = (await RulesAsync(mailbox)).Single(r => r.ProviderFilterId == "f1");
        await RenameAsync(edited.Id, "My name");

        _provider.Filters.RemoveAll(f => f.ProviderFilterId == "f1");
        _provider.Filters.Add(new RemoteFilter("f9", "from: bo", [new(RuleConditionKinds.From, "bo")],
            [new(RuleActionKinds.MarkRead, null)], "{}", IsReadOnly: false));
        await SyncAsync(mailbox);

        var rules = await RulesAsync(mailbox);
        Assert.Equal(2, rules.Count);
        var rule = rules.Single(r => r.Id == edited.Id);
        Assert.Equal("f9", rule.ProviderFilterId);
        Assert.Equal("My name", rule.Name);
        Assert.Equal(edited.Position, rule.Position);
        Assert.Equal("bo", Assert.Single(rule.Conditions).Value);
        Assert.Equal(RuleActionKinds.MarkRead, Assert.Single(rule.Actions).Kind);
    }

    [Fact]
    public async Task An_edit_can_turn_a_rule_read_only_and_back()
    {
        _provider.Filters.Add(Star("f1"));
        var mailbox = await ConnectAsync();
        var id = Assert.Single(await RulesAsync(mailbox)).Id;

        _provider.Filters.Clear();
        _provider.Filters.Add(new RemoteFilter("f2", "size: 100", [], [], """{"id": "f2"}""", IsReadOnly: true));
        await SyncAsync(mailbox);

        var readOnly = Assert.Single(await RulesAsync(mailbox));
        Assert.Equal(id, readOnly.Id);
        Assert.Equal(RuleStatus.ReadOnly, readOnly.Status);
        Assert.Empty(readOnly.Actions);

        _provider.Filters.Clear();
        _provider.Filters.Add(Star("f3"));
        await SyncAsync(mailbox);

        var back = Assert.Single(await RulesAsync(mailbox));
        Assert.Equal(id, back.Id);
        Assert.Equal(RuleStatus.Ok, back.Status);
        Assert.Null(back.ProviderFilterRaw);
    }

    [Fact]
    public async Task Two_gone_and_one_new_is_a_delete_and_an_import()
    {
        _provider.Filters.Add(Star("f1"));
        _provider.Filters.Add(Star("f2"));
        var mailbox = await ConnectAsync();
        var before = (await RulesAsync(mailbox)).Select(r => r.Id).ToList();

        _provider.Filters.Clear();
        _provider.Filters.Add(Star("f3"));
        await SyncAsync(mailbox);

        var rule = Assert.Single(await RulesAsync(mailbox));
        Assert.Equal("f3", rule.ProviderFilterId);
        Assert.DoesNotContain(rule.Id, before);
    }

    [Fact]
    public async Task List_failing_leaves_the_rules_and_the_cycle_alone_and_is_kept_until_it_works()
    {
        _provider.Filters.Add(Star("f1"));
        var mailbox = await ConnectAsync();

        _provider.FailFiltersWith = MailErrors.ProviderFailed("Filters are down");
        _provider.Filters.Clear();
        _provider.Deliver("m1", "New");
        var failed = await SyncAsync(mailbox);

        Assert.Single(await RulesAsync(mailbox));
        Assert.Equal("Filters are down", failed.FilterSyncError);
        Assert.NotEqual(MailboxStatus.Error, failed.Status);
        Assert.Null(failed.LastError);
        Assert.Equal(0, failed.ConsecutiveFailures);
        Assert.NotNull(await MessageAsync(mailbox, "m1"));

        _provider.FailFiltersWith = null;
        Assert.Null((await SyncAsync(mailbox)).FilterSyncError);
    }

    [Fact]
    public async Task A_throw_in_the_filter_sync_does_not_fail_the_cycle_and_shows_only_the_fixed_text()
    {
        var mailbox = await ConnectAsync();

        _provider.ThrowOnFilters = new InvalidOperationException("secret detail");
        var saved = await SyncAsync(mailbox);

        Assert.Equal(MailErrors.SyncFailed.Message, saved.FilterSyncError);
        Assert.Null(saved.LastError);
        Assert.Equal(0, saved.ConsecutiveFailures);
    }

    [Fact]
    public async Task A_provider_without_filters_is_not_asked()
    {
        _provider.Capabilities = new MailboxCapabilities(true, false, true);
        _provider.Filters.Add(Star("f1"));

        var mailbox = await ConnectAsync();

        Assert.Equal(0, _provider.ListFiltersCalls);
        Assert.Empty(await RulesAsync(mailbox));
    }

    private static RemoteFilter Star(string id) =>
        new(id, $"from: {id}", [new(RuleConditionKinds.From, id)], [new(RuleActionKinds.Star, null)], "{}", IsReadOnly: false);

    private async Task<Mailbox> ConnectAsync()
    {
        var mailbox = Mailbox.Connect(Guid.CreateVersion7(), MailProvider.Gmail, Guid.CreateVersion7(), "me@gmail.test", null);
        await RunAsync((ctx, ct) => ctx.AcquireRepository<IMailboxRepository>().AddAsync(mailbox, ct));
        return await SyncAsync(mailbox);
    }

    private async Task<Mailbox> SyncAsync(Mailbox mailbox)
    {
        using (var scope = _host.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SyncMailboxCommand(new SyncMailboxInput(mailbox.Id)));

        Mailbox? saved = null;
        await RunAsync(async (ctx, ct) => saved = await ctx.AcquireRepository<IMailboxRepository>().GetByIdAsync(mailbox.Id, ct));
        return saved!;
    }

    private async Task<List<MailRule>> RulesAsync(Mailbox mailbox)
    {
        List<MailRule> rules = [];
        await RunAsync(async (ctx, ct) => rules = [.. await ctx.AcquireRepository<IMailRuleRepository>().ListByMailboxAsync(mailbox.Id, ct)]);
        return rules;
    }

    private async Task<Guid> LabelIdAsync(Mailbox mailbox, string name)
    {
        Guid id = default;
        await RunAsync(async (ctx, ct) =>
            id = (await ctx.AcquireRepository<IMailLabelRepository>().ListByMailboxAsync(mailbox.Id, ct)).Single(l => l.Name == name).Id);
        return id;
    }

    private Task RenameAsync(Guid ruleId, string name) =>
        RunAsync(async (ctx, ct) =>
        {
            var rules = ctx.AcquireRepository<IMailRuleRepository>();
            var tracked = await rules.GetByIdAsync(ruleId, ct);
            tracked!.Update(name, tracked.Conditions, tracked.Actions);
            await rules.UpdateAsync(tracked, ct);
        });

    private async Task<MailMessage?> MessageAsync(Mailbox mailbox, string providerMessageId)
    {
        MailMessage? message = null;
        await RunAsync(async (ctx, ct) =>
            message = await ctx.AcquireRepository<IMailMessageRepository>().FindByProviderIdAsync(mailbox.Id, providerMessageId, ct));
        return message;
    }

    private async Task RunAsync(Func<IDataContext, CancellationToken, Task> work)
    {
        using var scope = _host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>().ExecuteAsync(CommunicationsModule.DatabaseKey, work);
    }
}
