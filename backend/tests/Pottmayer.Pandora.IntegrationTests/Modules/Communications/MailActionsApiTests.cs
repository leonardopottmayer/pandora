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

/// <summary>Organizing mail through the API, over a scripted provider (registered under <c>titan</c>).</summary>
[Collection("Integration")]
public sealed class MailActionsApiTests : IAsyncLifetime
{
    private const string Base = "/api/v1/communications";

    private readonly PandoraWebApplicationFactory _factory;
    private readonly FakeMailProvider _provider = new();
    private readonly WebApplicationFactory<Program> _host;

    public MailActionsApiTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddKeyedSingleton<IMailboxProvider>(MailProvider.Titan.Value, _provider)));
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record MailboxItem(Guid Id);
    private sealed record LabelItem(Guid Id, string Name, string? Role, int Total);
    private sealed record MessageItem(Guid Id, string Subject, bool IsUnread, bool IsStarred, List<Guid> LabelIds);
    private sealed record PageItem(List<MessageItem> Items, string? NextCursor);

    [Fact]
    public async Task Flags_go_to_the_provider_and_then_to_the_row()
    {
        _provider.Deliver("m1", "Hello");
        var (client, mailbox) = await SyncedMailboxAsync("flags");
        var message = await OnlyMessageAsync(client);
        Assert.True(message.IsUnread);

        var read = Assert.Single(await ActAsync(client, mailbox, "mark-read", [message.Id]));
        Assert.False(read.IsUnread);
        Assert.False((await OnlyMessageAsync(client)).IsUnread);

        var starred = Assert.Single(await ActAsync(client, mailbox, "star", [message.Id]));
        Assert.True(starred.IsStarred);
        Assert.False(starred.IsUnread);

        Assert.True(Assert.Single(await ActAsync(client, mailbox, "mark-unread", [message.Id])).IsUnread);
        Assert.False(Assert.Single(await ActAsync(client, mailbox, "unstar", [message.Id])).IsStarred);

        Assert.Equal(["flags:m1:False:", "flags:m1::True", "flags:m1:True:", "flags:m1::False"], _provider.Writes);
    }

    [Fact]
    public async Task A_provider_failure_changes_nothing_locally()
    {
        _provider.Deliver("m1", "Hello");
        var (client, mailbox) = await SyncedMailboxAsync("failure");
        var message = await OnlyMessageAsync(client);

        _provider.FailWith = MailErrors.ProviderFailed("Gmail answered 500");
        var response = await SendAsync(client, mailbox, "mark-read", [message.Id]);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("Gmail answered 500", await response.Content.ReadAsStringAsync());
        _provider.FailWith = null;
        Assert.True((await OnlyMessageAsync(client)).IsUnread);
    }

    [Fact]
    public async Task Someone_elses_mailbox_or_message_is_not_found_and_the_provider_never_hears_of_it()
    {
        _provider.Deliver("m1", "Private");
        var (owner, mailbox) = await SyncedMailboxAsync("owner");
        var message = await OnlyMessageAsync(owner);
        var (stranger, own) = await SyncedMailboxAsync("stranger");
        _provider.Writes.Clear();

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(stranger, mailbox, "mark-read", [message.Id])).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(stranger, own, "mark-read", [message.Id])).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync($"{Base}/mailboxes/{mailbox}/labels", new { name = "x" })).StatusCode);

        var label = (await LabelsAsync(owner, mailbox)).Single(l => l.Name == "INBOX");
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PatchAsJsonAsync($"{Base}/labels/{label.Id}", new { name = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"{Base}/labels/{label.Id}")).StatusCode);

        Assert.Empty(_provider.Writes);
    }

    [Fact]
    public async Task A_message_of_another_mailbox_in_the_list_stops_the_whole_action()
    {
        _provider.Deliver("m1", "One");
        var (client, first) = await SyncedMailboxAsync("two-boxes");
        var message = await OnlyMessageAsync(client);

        var otherAccount = await AccountsApi.ReadDataAsync<Guid>(await client.PutAsJsonAsync(
            "/api/v1/integrations/titan/password", new { username = "other@titan.test", password = "secret" }));
        var second = await AccountsApi.ReadDataAsync<MailboxItem>(await client.PostAsJsonAsync(
            $"{Base}/mailboxes", new { provider = "titan", externalAccountId = otherAccount }));
        _provider.Writes.Clear();

        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, second.Id, "mark-read", [message.Id])).StatusCode);
        Assert.Empty(_provider.Writes);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, first, "mark-read", [message.Id])).StatusCode);
    }

    [Fact]
    public async Task A_bad_request_is_refused_before_the_provider()
    {
        _provider.Deliver("m1", "Hello");
        var (client, mailbox) = await SyncedMailboxAsync("bad");
        var message = await OnlyMessageAsync(client);
        _provider.Writes.Clear();

        var tooMany = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendAsync(client, mailbox, "mark-read", tooMany)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendAsync(client, mailbox, "mark-read", [])).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendAsync(client, mailbox, "delete-forever", [message.Id])).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, mailbox, "move", [message.Id], Guid.NewGuid())).StatusCode);

        Assert.Empty(_provider.Writes);
    }

    [Fact]
    public async Task A_move_on_imap_gives_the_row_its_new_id_and_only_the_target_folder()
    {
        UseFolders();
        _provider.Labels.Add(new RemoteLabel("Projects", "Projects", LabelKind.User, null, IsExclusive: true));
        _provider.Deliver("m1", "Moved");
        _provider.MovedId = _ => "m1-new";
        var (client, mailbox) = await SyncedMailboxAsync("move");
        var message = await OnlyMessageAsync(client);
        var target = (await LabelsAsync(client, mailbox)).Single(l => l.Name == "Projects");

        var moved = Assert.Single(await ActAsync(client, mailbox, "move", [message.Id], target.Id));

        Assert.Equal(message.Id, moved.Id);
        Assert.Equal([target.Id], moved.LabelIds);
        Assert.Equal(["move:m1:Projects"], _provider.Writes);

        // The new id is what the provider hears next.
        _provider.Writes.Clear();
        await ActAsync(client, mailbox, "mark-read", [message.Id]);
        Assert.Equal(["flags:m1-new:False:"], _provider.Writes);
    }

    [Fact]
    public async Task A_move_with_no_new_id_leaves_the_row_for_the_next_cycle_to_stitch()
    {
        UseFolders();
        _provider.Labels.Add(new RemoteLabel("Projects", "Projects", LabelKind.User, null, IsExclusive: true));
        _provider.SnapshotLabel = "INBOX";
        _provider.Deliver("m1", "Moved");
        _provider.MovedId = _ => null;
        var (client, mailbox) = await SyncedMailboxAsync("no-id");
        var message = await OnlyMessageAsync(client);
        var labels = await LabelsAsync(client, mailbox);
        var inbox = labels.Single(l => l.Name == "INBOX");
        var target = labels.Single(l => l.Name == "Projects");

        var unchanged = Assert.Single(await ActAsync(client, mailbox, "move", [message.Id], target.Id));
        Assert.Equal([inbox.Id], unchanged.LabelIds);

        _provider.Move("m1", "m1-new", "Projects");
        await client.PostAsync($"{Base}/mailboxes/{mailbox}/sync", null);

        var stitched = await OnlyMessageAsync(client);
        Assert.Equal(message.Id, stitched.Id);
        Assert.Equal([target.Id], stitched.LabelIds);
    }

    [Fact]
    public async Task A_move_on_gmail_takes_the_label_leaves_the_inbox_and_keeps_the_others()
    {
        _provider.Labels.Add(new RemoteLabel("Label_1", "Receipts", LabelKind.User, null, IsExclusive: false));
        _provider.Labels.Add(new RemoteLabel("Label_2", "Taxes", LabelKind.User, null, IsExclusive: false));
        _provider.Deliver("m1", "Bill");
        var (client, mailbox) = await SyncedMailboxAsync("gmail-move");
        var message = await OnlyMessageAsync(client);
        var labels = await LabelsAsync(client, mailbox);
        var receipts = labels.Single(l => l.Name == "Receipts");
        var taxes = labels.Single(l => l.Name == "Taxes");

        var labeled = Assert.Single(await ActAsync(client, mailbox, "add-label", [message.Id], receipts.Id));
        Assert.Contains(receipts.Id, labeled.LabelIds);

        var moved = Assert.Single(await ActAsync(client, mailbox, "move", [message.Id], taxes.Id));
        Assert.Equal(new[] { receipts.Id, taxes.Id }.Order(), moved.LabelIds.Order());

        var removed = Assert.Single(await ActAsync(client, mailbox, "remove-label", [message.Id], receipts.Id));
        Assert.Equal([taxes.Id], removed.LabelIds);
    }

    [Fact]
    public async Task Archive_leaves_the_inbox_on_gmail_and_moves_to_the_archive_folder_on_imap()
    {
        _provider.Deliver("m1", "Gmail way");
        var (gmail, gmailBox) = await SyncedMailboxAsync("archive-gmail");
        var message = await OnlyMessageAsync(gmail);

        Assert.Empty(Assert.Single(await ActAsync(gmail, gmailBox, "archive", [message.Id])).LabelIds);
        var inbox = (await LabelsAsync(gmail, gmailBox)).Single(l => l.Name == "INBOX");
        Assert.Equal(["labels:m1:+:-INBOX"], _provider.Writes);
        Assert.Equal(0, inbox.Total);

        UseFolders();
        _provider.Writes.Clear();
        var refused = await SendAsync(gmail, gmailBox, "archive", [message.Id]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Contains(MailErrors.NoArchiveFolder.Code, await refused.Content.ReadAsStringAsync());
        Assert.Empty(_provider.Writes);

        _provider.Labels.Add(new RemoteLabel("Archive", "Archive", LabelKind.System, LabelRole.Archive, IsExclusive: true));
        await gmail.PostAsync($"{Base}/mailboxes/{gmailBox}/sync", null);
        var archive = (await LabelsAsync(gmail, gmailBox)).Single(l => l.Role == "archive");

        Assert.Equal([archive.Id], Assert.Single(await ActAsync(gmail, gmailBox, "archive", [message.Id])).LabelIds);
        Assert.Equal(["move:m1:Archive"], _provider.Writes);
    }

    [Fact]
    public async Task Trash_leaves_the_message_only_in_the_trash()
    {
        _provider.Labels.Add(new RemoteLabel("Trash", "Trash", LabelKind.System, LabelRole.Trash, IsExclusive: true));
        _provider.Deliver("m1", "Binned");
        var (client, mailbox) = await SyncedMailboxAsync("trash");
        var message = await OnlyMessageAsync(client);
        var trash = (await LabelsAsync(client, mailbox)).Single(l => l.Role == "trash");

        Assert.Equal([trash.Id], Assert.Single(await ActAsync(client, mailbox, "trash", [message.Id])).LabelIds);
        Assert.Equal(["trash:m1"], _provider.Writes);
    }

    [Fact]
    public async Task System_labels_cannot_be_targets_changed_or_added()
    {
        _provider.Labels.Add(new RemoteLabel("SENT", "SENT", LabelKind.System, LabelRole.Sent, IsExclusive: false));
        _provider.Labels.Add(new RemoteLabel("CATEGORY_UPDATES", "CATEGORY_UPDATES", LabelKind.System, null, IsExclusive: false));
        _provider.Deliver("m1", "Hello");
        var (client, mailbox) = await SyncedMailboxAsync("system");
        var message = await OnlyMessageAsync(client);
        var labels = await LabelsAsync(client, mailbox);
        var sent = labels.Single(l => l.Name == "SENT");
        var category = labels.Single(l => l.Name == "CATEGORY_UPDATES");
        _provider.Writes.Clear();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendAsync(client, mailbox, "move", [message.Id], sent.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendAsync(client, mailbox, "move", [message.Id], category.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendAsync(client, mailbox, "add-label", [message.Id], sent.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PatchAsJsonAsync($"{Base}/labels/{sent.Id}", new { name = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.DeleteAsync($"{Base}/labels/{sent.Id}")).StatusCode);

        Assert.Empty(_provider.Writes);
    }

    [Fact]
    public async Task Labels_on_a_folder_mailbox_are_not_supported()
    {
        UseFolders();
        _provider.Labels.Add(new RemoteLabel("Projects", "Projects", LabelKind.User, null, IsExclusive: true));
        _provider.Deliver("m1", "Hello");
        var (client, mailbox) = await SyncedMailboxAsync("folders");
        var message = await OnlyMessageAsync(client);
        var target = (await LabelsAsync(client, mailbox)).Single(l => l.Name == "Projects");
        _provider.Writes.Clear();

        var refused = await SendAsync(client, mailbox, "add-label", [message.Id], target.Id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Contains(MailErrors.ActionNotSupported.Code, await refused.Content.ReadAsStringAsync());
        Assert.Empty(_provider.Writes);
    }

    [Fact]
    public async Task Labels_are_created_renamed_and_deleted_and_show_up_at_once()
    {
        var (client, mailbox) = await SyncedMailboxAsync("labels");

        var created = await AccountsApi.ReadDataAsync<List<LabelItem>>(
            await client.PostAsJsonAsync($"{Base}/mailboxes/{mailbox}/labels", new { name = "  pandora-test  " }));
        var label = created.Single(l => l.Name == "pandora-test");

        var renamed = await AccountsApi.ReadDataAsync<List<LabelItem>>(
            await client.PatchAsJsonAsync($"{Base}/labels/{label.Id}", new { name = "renamed" }));
        Assert.Equal(label.Id, renamed.Single(l => l.Name == "renamed").Id);

        var deleted = await AccountsApi.ReadDataAsync<List<LabelItem>>(await client.DeleteAsync($"{Base}/labels/{label.Id}"));
        Assert.DoesNotContain(deleted, l => l.Id == label.Id);

        Assert.Equal(["create:pandora-test", "rename:pandora-test:renamed", "delete:pandora-test"], _provider.Writes);

        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await client.PostAsJsonAsync($"{Base}/mailboxes/{mailbox}/labels", new { name = "   " })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await client.PostAsJsonAsync($"{Base}/mailboxes/{mailbox}/labels", new { name = new string('x', 101) })).StatusCode);
    }

    [Fact]
    public async Task A_folder_the_provider_refuses_to_delete_is_a_409_and_stays()
    {
        _provider.Labels.Add(new RemoteLabel("Projects", "Projects", LabelKind.User, null, IsExclusive: true));
        var (client, mailbox) = await SyncedMailboxAsync("not-empty");
        var label = (await LabelsAsync(client, mailbox)).Single(l => l.Name == "Projects");

        _provider.FailWith = MailErrors.LabelNotEmpty;
        var response = await client.DeleteAsync($"{Base}/labels/{label.Id}");
        _provider.FailWith = null;

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(await LabelsAsync(client, mailbox), l => l.Id == label.Id);
    }

    /// <summary>An IMAP-shaped provider: a folder is the label, and the inbox is one too.</summary>
    private void UseFolders()
    {
        _provider.Capabilities = new MailboxCapabilities(false, false, false);
        _provider.Labels[0] = _provider.Labels[0] with { IsExclusive = true };
    }

    private async Task<(HttpClient Client, Guid MailboxId)> SyncedMailboxAsync(string name)
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

    private static async Task<MessageItem> OnlyMessageAsync(HttpClient client) =>
        Assert.Single((await AccountsApi.ReadDataAsync<PageItem>(await client.GetAsync($"{Base}/messages"))).Items);

    private static async Task<List<LabelItem>> LabelsAsync(HttpClient client, Guid mailbox) =>
        await AccountsApi.ReadDataAsync<List<LabelItem>>(await client.GetAsync($"{Base}/mailboxes/{mailbox}/labels"));

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, Guid mailbox, string action, Guid[] messageIds, Guid? labelId = null) =>
        client.PostAsJsonAsync($"{Base}/messages/actions", new { mailboxId = mailbox, messageIds, action, labelId });

    private static async Task<List<MessageItem>> ActAsync(HttpClient client, Guid mailbox, string action, Guid[] messageIds, Guid? labelId = null) =>
        await AccountsApi.ReadDataAsync<List<MessageItem>>(await SendAsync(client, mailbox, action, messageIds, labelId));
}
