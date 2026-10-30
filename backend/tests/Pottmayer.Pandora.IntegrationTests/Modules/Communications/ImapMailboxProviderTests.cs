using System.Net.Http.Json;
using MailKit;
using MailKit.Search;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Pottmayer.Pandora.IntegrationTests.Modules.Integrations;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
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
