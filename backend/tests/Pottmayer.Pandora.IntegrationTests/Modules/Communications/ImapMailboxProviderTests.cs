using System.Net.Http.Json;
using MailKit;
using MailKit.Search;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Pottmayer.Pandora.IntegrationTests.Modules.Integrations;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.Errors;
using Pottmayer.Pandora.Modules.Communications.Domain.Ports;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Communications;

/// <summary>
/// The IMAP adapter against a real server, logged in with a password account saved through Integrations.
/// </summary>
[Collection("Integration")]
public sealed class ImapMailboxProviderTests(PandoraWebApplicationFactory factory, GreenMailFixture mail)
    : IClassFixture<GreenMailFixture>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_wrong_password_is_an_auth_failure()
    {
        var mailbox = await ConnectMailboxAsync("auth@example.com", "not-the-password");

        var result = await WithProviderAsync(p => p.TestConnectionAsync(mailbox));

        Assert.False(result.IsSuccess);
        Assert.Equal("Communications.MailboxAuthFailed", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task Backfill_then_new_read_moved_and_deleted_messages()
    {
        const string user = "sync@example.com";
        var first = await mail.DeliverAsync(user, "First");
        await mail.DeliverAsync(user, "Second", inReplyTo: first);
        await mail.DeliverAsync(user, "Third");
        var mailbox = await ConnectMailboxAsync(user, GreenMailFixture.Password);

        Assert.True((await WithProviderAsync(p => p.TestConnectionAsync(mailbox))).IsSuccess);

        var labels = Value(await WithProviderAsync(p => p.ListLabelsAsync(mailbox)));
        var inbox = Assert.Single(labels);
        Assert.Equal(LabelRole.Inbox, inbox.Role);
        Assert.True(inbox.IsExclusive);

        // Backfill: newest first, in batches, until the cursor runs out.
        var start = Value(await WithProviderAsync(p => p.StartBackfillAsync(mailbox)));
        Assert.Equal(3, start.TotalEstimate);
        Assert.NotNull(start.BackfillCursor);

        var batch1 = Value(await WithProviderAsync(p => p.FetchBackfillBatchAsync(mailbox, start.BackfillCursor!, 2)));
        Assert.Equal(["Third", "Second"], batch1.Messages.Select(m => m.Subject));
        Assert.NotNull(batch1.NextCursor);

        var batch2 = Value(await WithProviderAsync(p => p.FetchBackfillBatchAsync(mailbox, batch1.NextCursor!, 2)));
        var firstMessage = Assert.Single(batch2.Messages);
        Assert.Equal("First", firstMessage.Subject);
        Assert.Null(batch2.NextCursor);

        var second = batch1.Messages[1];
        Assert.Equal(firstMessage.ThreadKey, second.ThreadKey);
        Assert.Equal("ana@sender.test", firstMessage.From!.Address);
        Assert.Equal(user, Assert.Single(firstMessage.To).Address);
        Assert.True(firstMessage.IsUnread);
        Assert.Equal(["INBOX"], firstMessage.ProviderLabelIds);

        // A message that arrives after the backfill started is new.
        await mail.DeliverAsync(user, "Fourth");
        var changes1 = Value(await WithProviderAsync(p => p.FetchChangesAsync(mailbox, start.SyncCursor)));
        Assert.Contains(changes1.Upserts, m => m.Subject == "Fourth");
        Assert.Equal(4, Assert.Single(changes1.Snapshots).ProviderMessageIds.Count);

        // Read in the webmail → read in Pandora; opening it in Pandora does not mark it as read.
        var raw = Value(await WithProviderAsync(p => p.GetRawMessageAsync(mailbox, batch1.Messages[0].ProviderMessageId)));
        Assert.Equal("Third", MimeMessage.Load(new MemoryStream(raw)).Subject);

        using (var webmail = await mail.ConnectAsync(user))
        {
            await webmail.Inbox.OpenAsync(FolderAccess.ReadWrite);
            var firstUid = (await webmail.Inbox.SearchAsync(SearchQuery.SubjectContains("First"))).Single();
            await webmail.Inbox.AddFlagsAsync(firstUid, MessageFlags.Seen, silent: true);
        }

        var changes2 = Value(await WithProviderAsync(p => p.FetchChangesAsync(mailbox, changes1.SyncCursor)));
        Assert.False(changes2.Upserts.Single(m => m.Subject == "First").IsUnread);
        Assert.True(changes2.Upserts.Single(m => m.Subject == "Third").IsUnread);

        // Moved to a new folder and deleted in the webmail.
        using (var webmail = await mail.ConnectAsync(user))
        {
            var archive = await webmail.GetFolder(webmail.PersonalNamespaces[0]).CreateAsync("Archive", isMessageFolder: true);
            await webmail.Inbox.OpenAsync(FolderAccess.ReadWrite);
            var thirdUid = (await webmail.Inbox.SearchAsync(SearchQuery.SubjectContains("Third"))).Single();
            await webmail.Inbox.MoveToAsync(thirdUid, archive);
            var secondUid = (await webmail.Inbox.SearchAsync(SearchQuery.SubjectContains("Second"))).Single();
            await webmail.Inbox.AddFlagsAsync(secondUid, MessageFlags.Deleted, silent: true);
            await webmail.Inbox.ExpungeAsync();
        }

        var changes3 = Value(await WithProviderAsync(p => p.FetchChangesAsync(mailbox, changes2.SyncCursor)));
        var moved = changes3.Upserts.Single(m => m.Subject == "Third");
        Assert.Equal(["Archive"], moved.ProviderLabelIds);
        Assert.Equal(first.Trim('<', '>'), firstMessage.ThreadKey);

        var inboxNow = changes3.Snapshots.Single(s => s.ProviderLabelId == "INBOX").ProviderMessageIds;
        Assert.Equal(2, inboxNow.Count);
        Assert.DoesNotContain(second.ProviderMessageId, inboxNow);
        Assert.DoesNotContain(batch1.Messages[0].ProviderMessageId, inboxNow);
        Assert.Single(changes3.Snapshots.Single(s => s.ProviderLabelId == "Archive").ProviderMessageIds);

        var labelsNow = Value(await WithProviderAsync(p => p.ListLabelsAsync(mailbox)));
        Assert.Equal(["INBOX", "Archive"], labelsNow.Select(l => l.ProviderLabelId));
    }

    [Fact]
    public async Task Read_and_star_are_seen_from_another_client_and_can_be_undone()
    {
        const string user = "flags@example.com";
        await mail.DeliverAsync(user, "Flagged");
        var mailbox = await ConnectMailboxAsync(user, GreenMailFixture.Password);
        var id = (await BackfillAsync(mailbox)).Single().ProviderMessageId;

        Value(await WithProviderAsync(p => p.SetFlagsAsync(mailbox, [id], isUnread: false, isStarred: true)));
        Assert.Equal(MessageFlags.Seen | MessageFlags.Flagged, await FlagsSeenByWebmailAsync(user));

        Value(await WithProviderAsync(p => p.SetFlagsAsync(mailbox, [id], isUnread: null, isStarred: false)));
        Assert.Equal(MessageFlags.Seen, await FlagsSeenByWebmailAsync(user));

        Value(await WithProviderAsync(p => p.SetFlagsAsync(mailbox, [id], isUnread: true, isStarred: null)));
        Assert.Equal(MessageFlags.None, await FlagsSeenByWebmailAsync(user));
    }

    [Fact]
    public async Task A_move_leaves_the_source_reaches_the_target_and_says_the_new_id()
    {
        const string user = "move@example.com";
        await mail.DeliverAsync(user, "One");
        await mail.DeliverAsync(user, "Two");
        await mail.DeliverAsync(user, "Stays");
        var mailbox = await ConnectMailboxAsync(user, GreenMailFixture.Password);
        var created = Value(await WithProviderAsync(p => p.CreateLabelAsync(mailbox, "Archive2")));
        var messages = await BackfillAsync(mailbox);
        var ids = messages.Where(m => m.Subject != "Stays").Select(m => m.ProviderMessageId).ToList();

        var moved = Value(await WithProviderAsync(p => p.MoveAsync(mailbox, ids, created.ProviderLabelId)));

        Assert.Equal(ids.Order(), moved.Keys.Order());
        Assert.All(moved.Values, next => Assert.EndsWith($":{created.ProviderLabelId}", next));
        Assert.Equal(["Stays"], await SubjectsSeenByWebmailAsync(user, "INBOX"));
        Assert.Equal(["One", "Two"], (await SubjectsSeenByWebmailAsync(user, created.ProviderLabelId)).Order());

        // The new ids work: the message opens from there.
        var raw = Value(await WithProviderAsync(p => p.GetRawMessageAsync(mailbox, moved.Values.First()!)));
        Assert.NotEmpty(raw);

        // Moving to where it already is changes nothing.
        var same = Value(await WithProviderAsync(p => p.MoveAsync(mailbox, [moved.Values.First()!], created.ProviderLabelId)));
        Assert.Equal(moved.Values.First(), same.Values.Single());
        Assert.Equal(2, (await SubjectsSeenByWebmailAsync(user, created.ProviderLabelId)).Count);
    }

    [Fact]
    public async Task Trash_without_a_folder_marked_as_trash_is_refused_and_keeps_the_message()
    {
        // GreenMail has no SPECIAL-USE, so no folder there can have the role, not even one named Trash. The move
        // itself is MoveAsync's; the real check on Titan covers finding the folder.
        const string user = "trash@example.com";
        await mail.DeliverAsync(user, "Kept");
        var mailbox = await ConnectMailboxAsync(user, GreenMailFixture.Password);
        var id = (await BackfillAsync(mailbox)).Single().ProviderMessageId;
        Value(await WithProviderAsync(p => p.CreateLabelAsync(mailbox, "Trash")));

        var refused = await WithProviderAsync(p => p.TrashAsync(mailbox, [id]));

        Assert.Equal(MailErrors.NoTrashFolder.Code, Assert.Single(refused.Errors).Code);
        Assert.Equal(["Kept"], await SubjectsSeenByWebmailAsync(user, "INBOX"));
    }

    [Fact]
    public async Task Folders_are_created_renamed_and_deleted_only_when_empty()
    {
        const string user = "folders@example.com";
        await mail.DeliverAsync(user, "Inside");
        var mailbox = await ConnectMailboxAsync(user, GreenMailFixture.Password);

        var keep = Value(await WithProviderAsync(p => p.CreateLabelAsync(mailbox, "Keep")));
        var work = Value(await WithProviderAsync(p => p.CreateLabelAsync(mailbox, "Work")));
        Assert.Equal(LabelKind.User, work.Kind);
        Assert.True(work.IsExclusive);
        Assert.Contains(work.ProviderLabelId, await LabelIdsAsync(mailbox));

        using (var webmail = await mail.ConnectAsync(user))
        {
            var parent = await webmail.GetFolderAsync(work.ProviderLabelId);
            await parent.CreateAsync("Child", isMessageFolder: true);
        }

        // A rename keeps the folder and its subfolder in scope.
        Value(await WithProviderAsync(p => p.RenameLabelAsync(mailbox, work.ProviderLabelId, "Renamed")));
        var labels = await LabelIdsAsync(mailbox);
        Assert.DoesNotContain(work.ProviderLabelId, labels);
        Assert.Contains(labels, l => l == "Renamed");
        Assert.Contains(labels, l => l.StartsWith("Renamed", StringComparison.Ordinal) && l.EndsWith("Child", StringComparison.Ordinal));
        Assert.Contains(keep.ProviderLabelId, labels);

        // A name with the separator would create a hierarchy.
        var separator = await SeparatorAsync(user);
        var badName = await WithProviderAsync(p => p.CreateLabelAsync(mailbox, $"a{separator}b"));
        Assert.Equal(MailErrors.LabelNameInvalid.Code, Assert.Single(badName.Errors).Code);

        // A folder with a subfolder or with messages stays.
        var hasChild = await WithProviderAsync(p => p.DeleteLabelAsync(mailbox, "Renamed"));
        Assert.Equal(MailErrors.LabelNotEmpty.Code, Assert.Single(hasChild.Errors).Code);

        var id = (await BackfillAsync(mailbox)).Single().ProviderMessageId;
        var moved = Value(await WithProviderAsync(p => p.MoveAsync(mailbox, [id], keep.ProviderLabelId)));
        var hasMessage = await WithProviderAsync(p => p.DeleteLabelAsync(mailbox, keep.ProviderLabelId));
        Assert.Equal(MailErrors.LabelNotEmpty.Code, Assert.Single(hasMessage.Errors).Code);
        Assert.Equal(["Inside"], await SubjectsSeenByWebmailAsync(user, keep.ProviderLabelId));

        // The message can go back to the inbox from there.
        Value(await WithProviderAsync(p => p.MoveAsync(mailbox, [moved[id]!], "INBOX")));
        Assert.Equal(["Inside"], await SubjectsSeenByWebmailAsync(user, "INBOX"));

        // Empty, it goes. A never-used folder: GreenMail drops the connection on deleting one that held a message,
        // so that case is for the real check on Titan.
        var empty = Value(await WithProviderAsync(p => p.CreateLabelAsync(mailbox, "Empty")));
        Value(await WithProviderAsync(p => p.DeleteLabelAsync(mailbox, empty.ProviderLabelId)));
        Assert.DoesNotContain(empty.ProviderLabelId, await LabelIdsAsync(mailbox));

        // The inbox can be neither renamed nor deleted.
        var rename = await WithProviderAsync(p => p.RenameLabelAsync(mailbox, "INBOX", "Nope"));
        Assert.Equal(MailErrors.SystemLabel.Code, Assert.Single(rename.Errors).Code);
        var delete = await WithProviderAsync(p => p.DeleteLabelAsync(mailbox, "INBOX"));
        Assert.Equal(MailErrors.SystemLabel.Code, Assert.Single(delete.Errors).Code);
    }

    [Fact]
    public async Task Every_folder_stays_in_scope_whatever_is_subscribed()
    {
        const string user = "scope@example.com";
        var mailbox = await ConnectMailboxAsync(user, GreenMailFixture.Password);

        // A server with no subscriptions at all, like Titan.
        using (var webmail = await mail.ConnectAsync(user))
        {
            var root = webmail.GetFolder(webmail.PersonalNamespaces[0]);
            await root.CreateAsync("Unsubscribed", isMessageFolder: true);
        }

        // Creating a folder in Pandora must not make the others disappear.
        var created = Value(await WithProviderAsync(p => p.CreateLabelAsync(mailbox, "Mine")));
        Assert.Contains("Unsubscribed", await LabelIdsAsync(mailbox));
        Assert.Contains(created.ProviderLabelId, await LabelIdsAsync(mailbox));

        // Neither must a subscription made in another mail client.
        using (var webmail = await mail.ConnectAsync(user))
        {
            await (await webmail.GetFolderAsync(created.ProviderLabelId)).SubscribeAsync();
        }

        Assert.Contains("Unsubscribed", await LabelIdsAsync(mailbox));

        // Nor a rename.
        Value(await WithProviderAsync(p => p.RenameLabelAsync(mailbox, created.ProviderLabelId, "Renamed")));
        var labels = await LabelIdsAsync(mailbox);
        Assert.Contains("Unsubscribed", labels);
        Assert.Contains("Renamed", labels);
        Assert.DoesNotContain(created.ProviderLabelId, labels);
    }

    private async Task<IReadOnlyList<RemoteMessage>> BackfillAsync(Mailbox mailbox)
    {
        var messages = new List<RemoteMessage>();
        var cursor = Value(await WithProviderAsync(p => p.StartBackfillAsync(mailbox))).BackfillCursor;
        while (cursor is not null)
        {
            var current = cursor;
            var batch = Value(await WithProviderAsync(p => p.FetchBackfillBatchAsync(mailbox, current, 100)));
            messages.AddRange(batch.Messages);
            cursor = batch.NextCursor;
        }
        return messages;
    }

    private async Task<List<string>> LabelIdsAsync(Mailbox mailbox) =>
        Value(await WithProviderAsync(p => p.ListLabelsAsync(mailbox))).Select(l => l.ProviderLabelId).ToList();

    private async Task<char> SeparatorAsync(string user)
    {
        using var webmail = await mail.ConnectAsync(user);
        return webmail.GetFolder(webmail.PersonalNamespaces[0]).DirectorySeparator;
    }

    private async Task<MessageFlags> FlagsSeenByWebmailAsync(string user)
    {
        using var webmail = await mail.ConnectAsync(user);
        await webmail.Inbox.OpenAsync(FolderAccess.ReadOnly);
        var summary = (await webmail.Inbox.FetchAsync(0, -1, MessageSummaryItems.Flags)).Single();
        return summary.Flags!.Value & (MessageFlags.Seen | MessageFlags.Flagged);
    }

    private async Task<List<string>> SubjectsSeenByWebmailAsync(string user, string path)
    {
        using var webmail = await mail.ConnectAsync(user);
        var folder = path == "INBOX" ? webmail.Inbox : await webmail.GetFolderAsync(path);
        await folder.OpenAsync(FolderAccess.ReadOnly);
        if (folder.Count == 0)
            return [];
        return (await folder.FetchAsync(0, -1, MessageSummaryItems.Envelope)).Select(s => s.Envelope.Subject).ToList();
    }

    /// <summary>A Pandora user with a password account for <paramref name="user"/>, and a mailbox on it.</summary>
    private async Task<Mailbox> ConnectMailboxAsync(string user, string password)
    {
        var owner = $"owner-{Guid.NewGuid():N}@example.com";
        await IdentityHelper.AuthenticateAsync(_client, factory.ConnectionString, owner, owner.Split('@')[0][..20]);
        var userId = await AccountsApi.UserIdAsync(factory.ConnectionString, owner);

        var response = await _client.PutAsJsonAsync("/api/v1/integrations/imap/password", new { username = user, password });
        var accountId = await AccountsApi.ReadDataAsync<Guid>(response);

        return Mailbox.Connect(userId, MailProvider.Imap, accountId, user, null,
            new MailServerSettings(mail.Host, mail.MappedImapPort, mail.Host, mail.MappedSmtpPort));
    }

    private async Task<T> WithProviderAsync<T>(Func<IMailboxProvider, Task<T>> call)
    {
        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredKeyedService<IMailboxProvider>(MailProvider.Imap.Value);
        return await call(provider);
    }

    private static T Value<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}")));
        return result.Value;
    }
}
