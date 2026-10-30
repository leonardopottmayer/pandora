using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Modules.Integrations;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Communications;

/// <summary>The <c>/communications</c> API over a scripted provider (registered under <c>titan</c>).</summary>
[Collection("Integration")]
public sealed class CommunicationsApiTests : IAsyncLifetime
{
    private const string Base = "/api/v1/communications";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly FakeMailProvider _provider = new();
    private readonly WebApplicationFactory<Program> _host;

    public CommunicationsApiTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddKeyedSingleton<IMailboxProvider>(MailProvider.Titan.Value, _provider)));
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record MailboxItem(Guid Id, string Provider, string Address, string Status, string? LastError, int MessageCount, int? MessageTotalEstimate);
    private sealed record LabelItem(Guid Id, string Name, int Total, int Unread);
    private sealed record MessageItem(Guid Id, string Subject, DateTimeOffset SentAt);
    private sealed record PageItem(List<MessageItem> Items, string? NextCursor);
    private sealed record AttachmentItem(string Id, string? FileName, string ContentType, long SizeBytes);
    private sealed record OpenedItem(MessageItem Message, string? TextBody, string? HtmlBody, List<AttachmentItem> Attachments);

    [Fact]
    public async Task A_refused_connection_test_is_a_422_and_creates_nothing()
    {
        var (client, accountId) = await SignInWithTitanAsync("refused");
        _provider.FailWith = MailErrors.MailboxAuthFailed("Invalid credentials");

        var response = await client.PostAsJsonAsync($"{Base}/mailboxes", new { provider = "titan", externalAccountId = accountId });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("Invalid credentials", await response.Content.ReadAsStringAsync());
        Assert.Empty(await AccountsApi.ReadDataAsync<List<MailboxItem>>(await client.GetAsync($"{Base}/mailboxes")));
    }

    [Fact]
    public async Task An_account_of_another_provider_or_already_used_is_refused()
    {
        var (client, accountId) = await SignInWithTitanAsync("kinds");

        var asGmail = await client.PostAsJsonAsync($"{Base}/mailboxes", new { provider = "gmail", externalAccountId = accountId });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, asGmail.StatusCode);

        await ConnectAsync(client, accountId);
        var again = await client.PostAsJsonAsync($"{Base}/mailboxes", new { provider = "titan", externalAccountId = accountId });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task The_message_list_pages_newest_first()
    {
        _provider.Deliver("m1", "Oldest", sentAt: DateTimeOffset.UtcNow.AddDays(-3));
        _provider.Deliver("m2", "Middle", sentAt: DateTimeOffset.UtcNow.AddDays(-2));
        _provider.Deliver("m3", "Newest", sentAt: DateTimeOffset.UtcNow.AddDays(-1));
        var (client, accountId) = await SignInWithTitanAsync("paging");
        var mailbox = await ConnectAsync(client, accountId);
        Assert.Equal("me@titan.test", mailbox.Address);

        var synced = await AccountsApi.ReadDataAsync<MailboxItem>(await client.PostAsync($"{Base}/mailboxes/{mailbox.Id}/sync", null));
        Assert.Equal((3, 3), (synced.MessageCount, synced.MessageTotalEstimate!.Value));

        var first = await PageAsync(client, $"mailboxId={mailbox.Id}&limit=2");
        Assert.Equal(["Newest", "Middle"], first.Items.Select(m => m.Subject));
        Assert.NotNull(first.NextCursor);

        var second = await PageAsync(client, $"limit=2&before={Uri.EscapeDataString(first.NextCursor!)}");
        Assert.Equal(["Oldest"], second.Items.Select(m => m.Subject));
        Assert.Null(second.NextCursor);

        Assert.Equal(["Middle"], (await PageAsync(client, "q=midd")).Items.Select(m => m.Subject));

        var inbox = Assert.Single(await AccountsApi.ReadDataAsync<List<LabelItem>>(await client.GetAsync($"{Base}/mailboxes/{mailbox.Id}/labels")));
        Assert.Equal(("INBOX", 3, 3), (inbox.Name, inbox.Total, inbox.Unread));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync($"{Base}/messages?before=garbage")).StatusCode);
    }

    [Fact]
    public async Task Opening_a_message_fetches_it_once_and_serves_its_attachment()
    {
        _provider.Deliver("m1", "Invoice");
        var (client, accountId) = await SignInWithTitanAsync("opening");
        var mailbox = await ConnectAsync(client, accountId);
        await client.PostAsync($"{Base}/mailboxes/{mailbox.Id}/sync", null);
        var messageId = Assert.Single((await PageAsync(client, "")).Items).Id;

        var opened = await AccountsApi.ReadDataAsync<OpenedItem>(await client.GetAsync($"{Base}/messages/{messageId}"));
        await AccountsApi.ReadDataAsync<OpenedItem>(await client.GetAsync($"{Base}/messages/{messageId}"));

        Assert.Equal(1, _provider.RawFetches);
        Assert.Contains("Text of Invoice", opened.TextBody);
        Assert.Contains("Html of Invoice", opened.HtmlBody);
        var attachment = Assert.Single(opened.Attachments);
        Assert.Equal(("invoice.pdf", "application/pdf"), (attachment.FileName, attachment.ContentType));

        var file = await client.GetAsync($"{Base}/messages/{messageId}/attachments/{attachment.Id}");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("%PDF-1.4 fake", await file.Content.ReadAsStringAsync());
        Assert.Equal("attachment", file.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/messages/{messageId}/attachments/99")).StatusCode);
    }

    [Fact]
    public async Task Another_users_mailbox_and_messages_are_not_found()
    {
        _provider.Deliver("m1", "Private");
        var (owner, accountId) = await SignInWithTitanAsync("owner");
        var mailbox = await ConnectAsync(owner, accountId);
        await owner.PostAsync($"{Base}/mailboxes/{mailbox.Id}/sync", null);
        var messageId = Assert.Single((await PageAsync(owner, "")).Items).Id;

        var (stranger, _) = await SignInWithTitanAsync("stranger");

        Assert.Empty((await PageAsync(stranger, "")).Items);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{Base}/messages?mailboxId={mailbox.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{Base}/mailboxes/{mailbox.Id}/labels")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"{Base}/mailboxes/{mailbox.Id}/sync", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PatchAsJsonAsync($"{Base}/mailboxes/{mailbox.Id}", new { displayName = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"{Base}/mailboxes/{mailbox.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{Base}/messages/{messageId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{Base}/messages/{messageId}/attachments/0")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync($"{Base}/mailboxes",
            new { provider = "titan", externalAccountId = accountId })).StatusCode);
    }

    [Fact]
    public async Task Sync_now_reconnects_a_stopped_mailbox_and_delete_removes_its_messages()
    {
        _provider.Deliver("m1", "Hello");
        var (client, accountId) = await SignInWithTitanAsync("lifecycle");
        var mailbox = await ConnectAsync(client, accountId);

        _provider.FailWith = MailErrors.MailboxAuthFailed("Invalid credentials");
        var stopped = await AccountsApi.ReadDataAsync<MailboxItem>(await client.PostAsync($"{Base}/mailboxes/{mailbox.Id}/sync", null));
        Assert.Equal("needs-reconnect", stopped.Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsync($"{Base}/mailboxes/{mailbox.Id}/sync", null)).StatusCode);

        _provider.FailWith = null;
        var back = await AccountsApi.ReadDataAsync<MailboxItem>(await client.PostAsync($"{Base}/mailboxes/{mailbox.Id}/sync", null));
        Assert.Equal(("active", null, 1), (back.Status, back.LastError, back.MessageCount));

        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"{Base}/mailboxes/{mailbox.Id}")).StatusCode);
        Assert.Empty(await AccountsApi.ReadDataAsync<List<MailboxItem>>(await client.GetAsync($"{Base}/mailboxes")));
        Assert.Empty((await PageAsync(client, "")).Items);
    }

    private async Task<(HttpClient Client, Guid AccountId)> SignInWithTitanAsync(string name)
    {
        var client = _host.CreateClient();
        await IdentityHelper.AuthenticateAsync(client, _factory.ConnectionString, $"{name}@example.com", name);
        var accountId = await AccountsApi.ReadDataAsync<Guid>(await client.PutAsJsonAsync(
            "/api/v1/integrations/titan/password", new { username = "me@titan.test", password = "secret" }));
        return (client, accountId);
    }

    private static async Task<MailboxItem> ConnectAsync(HttpClient client, Guid accountId) =>
        await AccountsApi.ReadDataAsync<MailboxItem>(await client.PostAsJsonAsync(
            $"{Base}/mailboxes", new { provider = "titan", externalAccountId = accountId }));

    private static async Task<PageItem> PageAsync(HttpClient client, string query) =>
        await AccountsApi.ReadDataAsync<PageItem>(await client.GetAsync($"{Base}/messages?{query}"));
}
