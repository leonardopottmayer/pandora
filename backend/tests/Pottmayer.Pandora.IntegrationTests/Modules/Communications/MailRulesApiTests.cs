using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Modules.Integrations;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Communications;

/// <summary>
/// Rules through the API. The scripted provider sits under <c>titan</c> since that account needs no OAuth; a test that says
/// so makes it run filters, and what matters then is that capability, the same one Gmail has.
/// </summary>
[Collection("Integration")]
public sealed class MailRulesApiTests : IAsyncLifetime
{
    private const string Base = "/api/v1/communications";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly FakeMailProvider _provider = new() { Capabilities = new MailboxCapabilities(false, true, false) };
    private readonly WebApplicationFactory<Program> _host;

    public MailRulesApiTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddKeyedSingleton<IMailboxProvider>(MailProvider.Titan.Value, _provider)));

        _provider.Labels.Add(new RemoteLabel("Work", "Work", LabelKind.User, null, IsExclusive: true));
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record MailboxItem(Guid Id, string? FilterSyncError);
    private sealed record LabelItem(Guid Id, string Name);
    private sealed record RuleItem(
        Guid Id, string Name, int Position, List<MailRuleCondition> Conditions, List<MailRuleAction> Actions,
        string Execution, bool IsEnabled, string Status, string? LastError, string? ProviderFilterRaw);

    private static readonly object[] FromAna = [new { kind = "from", value = "ana" }];
    private static readonly object[] FromBo = [new { kind = "from", value = "bo" }];
    private static readonly object[] Star = [new { kind = "star" }];

    [Fact]
    public async Task A_rule_on_a_mailbox_with_filters_becomes_a_filter_and_a_later_cycle_does_not_import_it()
    {
        var (client, mailbox) = await MailboxAsync("create");
        var work = await WorkAsync(client, mailbox);

        var rule = Assert.Single(await ReadAsync(await CreateAsync(client, mailbox, "Mine", FromAna, [new { kind = "move", labelId = work }])));

        Assert.Equal("provider-filter", rule.Execution);
        Assert.True(rule.IsEnabled);
        Assert.Equal(["create-filter:f1:Work"], _provider.Writes);

        await client.PostAsync($"{Base}/mailboxes/{mailbox}/sync", null);
        Assert.Equal(rule.Id, Assert.Single(await ListAsync(client, mailbox)).Id);
    }

    [Fact]
    public async Task A_rule_on_a_mailbox_without_filters_runs_in_pandora_and_calls_nothing()
    {
        _provider.Capabilities = new MailboxCapabilities(false, false, false);
        var (client, mailbox) = await MailboxAsync("engine");

        var rule = Assert.Single(await ReadAsync(await CreateAsync(client, mailbox, "Mine", FromAna, Star)));

        Assert.Equal("pandora", rule.Execution);
        Assert.Empty(_provider.Writes);

        var disabled = Assert.Single(await ReadAsync(await PutAsync(client, rule.Id, "Mine", FromBo, Star, isEnabled: false)));
        Assert.False(disabled.IsEnabled);
        Assert.Empty(await ReadAsync(await client.DeleteAsync($"{Base}/rules/{rule.Id}")));
        Assert.Empty(_provider.Writes);
    }

    [Fact]
    public async Task A_refused_rule_never_reaches_the_provider()
    {
        var (client, mailbox) = await MailboxAsync("refused");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await CreateAsync(client, mailbox, "Empty", [], Star)).StatusCode);

        Assert.Empty(_provider.Writes);
        Assert.Empty(await ListAsync(client, mailbox));
    }

    [Fact]
    public async Task Someone_elses_is_not_found_and_the_provider_never_hears_of_it()
    {
        var (owner, mailbox) = await MailboxAsync("owner");
        var rule = Assert.Single(await ReadAsync(await CreateAsync(owner, mailbox, "Mine", FromAna, Star)));
        var (stranger, _) = await MailboxAsync("stranger");
        _provider.Writes.Clear();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{Base}/mailboxes/{mailbox}/rules")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CreateAsync(stranger, mailbox, "x", FromAna, Star)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(stranger, rule.Id, "x", FromBo, Star)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"{Base}/rules/{rule.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsJsonAsync($"{Base}/mailboxes/{mailbox}/rules/order", new { ruleIds = new[] { rule.Id } })).StatusCode);

        Assert.Empty(_provider.Writes);
        Assert.Equal("Mine", Assert.Single(await ListAsync(owner, mailbox)).Name);
    }

    [Fact]
    public async Task A_read_only_rule_cannot_be_edited_nor_disabled()
    {
        _provider.Filters.Add(new RemoteFilter("f0", "size: 100", [], [], """{"id": "f0"}""", IsReadOnly: true));
        var (client, mailbox) = await MailboxAsync("readonly");
        var rule = Assert.Single(await ListAsync(client, mailbox));
        Assert.Equal("read-only", rule.Status);
        Assert.Contains("f0", rule.ProviderFilterRaw);

        var edit = await PutAsync(client, rule.Id, "New", FromAna, Star);
        var disable = await PutAsync(client, rule.Id, rule.Name, FromAna, Star, isEnabled: false);

        Assert.Contains(MailErrors.RuleReadOnly.Code, await edit.Content.ReadAsStringAsync());
        Assert.Contains(MailErrors.RuleReadOnly.Code, await disable.Content.ReadAsStringAsync());
        Assert.Empty(_provider.Writes);
    }

    [Fact]
    public async Task A_provider_failure_changes_nothing_and_its_message_is_shown()
    {
        var (client, mailbox) = await MailboxAsync("failure");
        var rule = Assert.Single(await ReadAsync(await CreateAsync(client, mailbox, "Mine", FromAna, Star)));

        // Gmail's own refusal, such as being over 1,000 filters.
        _provider.FailWith = MailErrors.ProviderFailed("Too many filters");
        var created = await CreateAsync(client, mailbox, "Other", FromBo, Star);
        var edited = await PutAsync(client, rule.Id, "New", FromBo, Star);
        var disabled = await PutAsync(client, rule.Id, "Mine", FromAna, Star, isEnabled: false);
        var deleted = await client.DeleteAsync($"{Base}/rules/{rule.Id}");
        _provider.FailWith = null;

        foreach (var response in new[] { created, edited, disabled, deleted })
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Contains("Too many filters", await response.Content.ReadAsStringAsync());
        }

        var saved = Assert.Single(await ListAsync(client, mailbox));
        Assert.Equal("Mine", saved.Name);
        Assert.Equal("ana", Assert.Single(saved.Conditions).Value);
        Assert.True(saved.IsEnabled);
        Assert.Equal("f1", Assert.Single(_provider.Filters).ProviderFilterId);
    }

    [Fact]
    public async Task No_filter_scope_is_its_own_error_and_does_not_stop_the_mailbox()
    {
        var (client, mailbox) = await MailboxAsync("scope");

        _provider.FailWith = MailErrors.MissingFilterScope;
        var response = await CreateAsync(client, mailbox, "Mine", FromAna, Star);
        _provider.FailWith = null;

        Assert.Contains(MailErrors.MissingFilterScope.Code, await response.Content.ReadAsStringAsync());
        Assert.Empty(await ListAsync(client, mailbox));
        Assert.Equal(mailbox, (await ReadMailboxAsync(client, mailbox)).Id);
    }

    [Fact]
    public async Task An_edit_creates_the_new_filter_before_deleting_the_old()
    {
        var (client, mailbox) = await MailboxAsync("edit");
        var rule = Assert.Single(await ReadAsync(await CreateAsync(client, mailbox, "Mine", FromAna, Star)));
        _provider.Writes.Clear();

        var edited = Assert.Single(await ReadAsync(await PutAsync(client, rule.Id, "Mine", FromBo, Star)));

        Assert.Equal("bo", Assert.Single(edited.Conditions).Value);
        Assert.Null(edited.LastError);
        Assert.Equal(["create-filter:f2:star", "delete-filter:f1"], _provider.Writes);
        Assert.Equal("f2", Assert.Single(_provider.Filters).ProviderFilterId);
    }

    [Fact]
    public async Task A_new_name_is_pandoras_own_and_gmail_does_not_hear_of_it()
    {
        var (client, mailbox) = await MailboxAsync("rename");
        var rule = Assert.Single(await ReadAsync(await CreateAsync(client, mailbox, "Mine", FromAna, Star)));
        _provider.Writes.Clear();

        var renamed = Assert.Single(await ReadAsync(await PutAsync(client, rule.Id, "Better", FromAna, Star)));

        Assert.Equal("Better", renamed.Name);
        Assert.Empty(_provider.Writes);
    }

    [Fact]
    public async Task An_edit_whose_delete_fails_keeps_the_new_filter_and_says_so()
    {
        var (client, mailbox) = await MailboxAsync("leftover");
        var rule = Assert.Single(await ReadAsync(await CreateAsync(client, mailbox, "Mine", FromAna, Star)));

        _provider.FailFilterDeleteWith = MailErrors.ProviderFailed("Gmail answered 500");
        var response = await PutAsync(client, rule.Id, "Mine", FromBo, Star);
        _provider.FailFilterDeleteWith = null;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var edited = Assert.Single(await ReadAsync(response));
        Assert.Equal("bo", Assert.Single(edited.Conditions).Value);
        Assert.Contains("previous filter", edited.LastError);

        // What was left over is a filter like any other: the next cycle shows it.
        await client.PostAsync($"{Base}/mailboxes/{mailbox}/sync", null);
        Assert.Equal(2, (await ListAsync(client, mailbox)).Count);
    }

    [Fact]
    public async Task Disabling_deletes_the_filter_and_keeps_the_rule_and_enabling_makes_it_again()
    {
        var (client, mailbox) = await MailboxAsync("toggle");
        var rule = Assert.Single(await ReadAsync(await CreateAsync(client, mailbox, "Mine", FromAna, Star)));

        var disabled = Assert.Single(await ReadAsync(await PutAsync(client, rule.Id, "Mine", FromAna, Star, isEnabled: false)));
        Assert.False(disabled.IsEnabled);
        Assert.Empty(_provider.Filters);

        // A cycle while disabled: the rule is not in the diff.
        await client.PostAsync($"{Base}/mailboxes/{mailbox}/sync", null);
        Assert.Single(await ListAsync(client, mailbox));

        var enabled = Assert.Single(await ReadAsync(await PutAsync(client, rule.Id, "Mine", FromAna, Star)));
        Assert.True(enabled.IsEnabled);
        Assert.Equal("f2", Assert.Single(_provider.Filters).ProviderFilterId);
    }

    [Fact]
    public async Task Deleting_goes_to_the_filter_first_also_for_a_read_only_one()
    {
        _provider.Filters.Add(new RemoteFilter("f0", "size: 100", [], [], """{"id": "f0"}""", IsReadOnly: true));
        var (client, mailbox) = await MailboxAsync("delete");
        var rule = Assert.Single(await ListAsync(client, mailbox));

        Assert.Empty(await ReadAsync(await client.DeleteAsync($"{Base}/rules/{rule.Id}")));

        Assert.Equal(["delete-filter:f0"], _provider.Writes);
        await client.PostAsync($"{Base}/mailboxes/{mailbox}/sync", null);
        Assert.Empty(await ListAsync(client, mailbox));
    }

    [Fact]
    public async Task Reordering_puts_the_listed_first_and_anything_else_after()
    {
        var (client, mailbox) = await MailboxAsync("order");
        await CreateAsync(client, mailbox, "A", FromAna, Star);
        await CreateAsync(client, mailbox, "B", FromBo, Star);
        var rules = await ReadAsync(await CreateAsync(client, mailbox, "C", [new { kind = "subject", value = "x" }], Star));

        var reordered = await ReadAsync(await client.PutAsJsonAsync(
            $"{Base}/mailboxes/{mailbox}/rules/order", new { ruleIds = new[] { rules[2].Id, rules[0].Id } }));

        Assert.Equal(["C", "A", "B"], reordered.Select(r => r.Name));
        Assert.Equal([0, 1, 2], reordered.Select(r => r.Position));
    }

    [Fact]
    public async Task A_cycle_during_a_save_does_not_import_the_filter_being_saved()
    {
        var (client, mailbox) = await MailboxAsync("race");
        var created = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        _provider.AfterFilterCreated = () =>
        {
            created.SetResult();
            return release.Task;
        };

        var saving = CreateAsync(client, mailbox, "Mine", FromAna, Star);
        await created.Task;
        var cycle = client.PostAsync($"{Base}/mailboxes/{mailbox}/sync", null);

        // Without the gate the cycle would be done by now, the filter imported.
        await Task.Delay(500);
        release.SetResult();

        Assert.Equal(HttpStatusCode.OK, (await saving).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cycle).StatusCode);
        Assert.Equal("Mine", Assert.Single(await ListAsync(client, mailbox)).Name);
    }

    [Fact]
    public async Task The_mailbox_shows_the_filter_sync_error()
    {
        var (client, mailbox) = await MailboxAsync("syncerror");

        _provider.FailFiltersWith = MailErrors.ProviderFailed("Filters are down");
        await client.PostAsync($"{Base}/mailboxes/{mailbox}/sync", null);

        Assert.Equal("Filters are down", (await ReadMailboxAsync(client, mailbox)).FilterSyncError);
    }

    private async Task<(HttpClient Client, Guid MailboxId)> MailboxAsync(string name)
    {
        var client = _host.CreateClient();
        await IdentityHelper.AuthenticateAsync(client, _factory.ConnectionString, $"{name}@example.com", name);
        var accountId = await AccountsApi.ReadDataAsync<Guid>(await client.PutAsJsonAsync(
            "/api/v1/integrations/titan/password", new { username = "me@titan.test", password = "secret" }));
        var mailbox = await AccountsApi.ReadDataAsync<MailboxItem>(await client.PostAsJsonAsync(
            $"{Base}/mailboxes", new { provider = "titan", externalAccountId = accountId }));
        await client.PostAsync($"{Base}/mailboxes/{mailbox.Id}/sync", null);
        return (client, mailbox.Id);
    }

    private static async Task<MailboxItem> ReadMailboxAsync(HttpClient client, Guid mailbox) =>
        (await AccountsApi.ReadDataAsync<List<MailboxItem>>(await client.GetAsync($"{Base}/mailboxes"))).Single(m => m.Id == mailbox);

    private static async Task<Guid> WorkAsync(HttpClient client, Guid mailbox) =>
        (await AccountsApi.ReadDataAsync<List<LabelItem>>(await client.GetAsync($"{Base}/mailboxes/{mailbox}/labels"))).Single(l => l.Name == "Work").Id;

    private static Task<HttpResponseMessage> CreateAsync(HttpClient client, Guid mailbox, string name, object[] conditions, object[] actions) =>
        client.PostAsJsonAsync($"{Base}/mailboxes/{mailbox}/rules", new { name, conditions, actions });

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, Guid rule, string name, object[] conditions, object[] actions, bool isEnabled = true) =>
        client.PutAsJsonAsync($"{Base}/rules/{rule}", new { name, conditions, actions, isEnabled });

    private static Task<List<RuleItem>> ReadAsync(HttpResponseMessage response) => AccountsApi.ReadDataAsync<List<RuleItem>>(response);

    private static async Task<List<RuleItem>> ListAsync(HttpClient client, Guid mailbox) =>
        await ReadAsync(await client.GetAsync($"{Base}/mailboxes/{mailbox}/rules"));
}
