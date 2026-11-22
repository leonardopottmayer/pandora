using Pottmayer.Pandora.Modules.Communications.Domain.Entities;
using Pottmayer.Pandora.Modules.Communications.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Communications.Tests;

public sealed class MailboxTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static Mailbox NewGmail() =>
        Mailbox.Connect(Guid.NewGuid(), MailProvider.Gmail, Guid.NewGuid(), " me@gmail.com ", "  ");

    [Fact]
    public void Connect_gmail_has_no_server_and_starts_syncing()
    {
        var mailbox = NewGmail();

        Assert.Equal("me@gmail.com", mailbox.Address);
        Assert.Null(mailbox.DisplayName);
        Assert.Null(mailbox.Server);
        Assert.Equal(MailboxStatus.Syncing, mailbox.Status);
        Assert.False(mailbox.IsBackfillComplete);
    }

    [Fact]
    public void Connect_gmail_ignores_server_settings()
    {
        var mailbox = Mailbox.Connect(Guid.NewGuid(), MailProvider.Gmail, Guid.NewGuid(), "me@gmail.com", null,
            new MailServerSettings("imap.example.com", 993, "smtp.example.com", 465));

        Assert.Null(mailbox.Server);
    }

    [Fact]
    public void Connect_titan_uses_the_preset_when_no_server_is_given()
    {
        var mailbox = Mailbox.Connect(Guid.NewGuid(), MailProvider.Titan, Guid.NewGuid(), "me@example.com", "Work");

        Assert.Equal(new MailServerSettings("imap.titan.email", 993, "smtp.titan.email", 465), mailbox.Server);
    }

    [Fact]
    public void Connect_imap_requires_server_settings()
    {
        Assert.Throws<ArgumentException>(() =>
            Mailbox.Connect(Guid.NewGuid(), MailProvider.Imap, Guid.NewGuid(), "me@example.com", null));

        var server = new MailServerSettings("mail.example.com", 143, "mail.example.com", 587);
        var mailbox = Mailbox.Connect(Guid.NewGuid(), MailProvider.Imap, Guid.NewGuid(), "me@example.com", null, server);
        Assert.Equal(server, mailbox.Server);
    }

    [Fact]
    public void Backfill_runs_until_the_cursor_is_null()
    {
        var mailbox = NewGmail();

        mailbox.StartBackfill("h1", "page-1", 1200, Now);
        Assert.Equal("h1", mailbox.SyncCursor);
        Assert.Equal("page-1", mailbox.BackfillCursor);
        Assert.Equal(1200, mailbox.MessageTotalEstimate);
        Assert.Equal(MailboxStatus.Syncing, mailbox.Status);

        mailbox.AdvanceBackfill("page-2", Now);
        Assert.False(mailbox.IsBackfillComplete);

        mailbox.AdvanceBackfill(null, Now);
        Assert.Equal(Now, mailbox.BackfillCompletedAt);
        Assert.Null(mailbox.BackfillCursor);
        Assert.Equal(MailboxStatus.Active, mailbox.Status);
    }

    [Fact]
    public void An_empty_mailbox_completes_its_backfill_at_once()
    {
        var mailbox = NewGmail();

        mailbox.StartBackfill("h1", backfillCursor: null, totalEstimate: 0, Now);

        Assert.True(mailbox.IsBackfillComplete);
        Assert.Equal(MailboxStatus.Active, mailbox.Status);
    }

    [Fact]
    public void Starting_over_reopens_a_completed_backfill()
    {
        var mailbox = NewGmail();
        mailbox.StartBackfill("h1", null, 0, Now);

        mailbox.StartBackfill("h9", "page-1", 10, Now);

        Assert.False(mailbox.IsBackfillComplete);
        Assert.Equal("h9", mailbox.SyncCursor);
        Assert.Equal(MailboxStatus.Syncing, mailbox.Status);
    }

    [Fact]
    public void Failures_turn_into_error_at_the_fifth_and_a_success_clears_them()
    {
        var mailbox = NewGmail();
        mailbox.StartBackfill("h1", null, 0, Now);

        for (var i = 1; i < Mailbox.MaxConsecutiveFailures; i++)
            mailbox.RecordFailure($"timeout {i}");
        Assert.Equal(MailboxStatus.Active, mailbox.Status);
        Assert.Equal(4, mailbox.ConsecutiveFailures);

        mailbox.RecordFailure("timeout 5");
        Assert.Equal(MailboxStatus.Error, mailbox.Status);
        Assert.Equal("timeout 5", mailbox.LastError);
        Assert.True(mailbox.CanSync);

        mailbox.RecordSuccess(Now);
        Assert.Equal(MailboxStatus.Active, mailbox.Status);
        Assert.Equal(0, mailbox.ConsecutiveFailures);
        Assert.Null(mailbox.LastError);
        Assert.Equal(Now, mailbox.LastSyncedAt);
    }

    [Fact]
    public void A_success_during_the_backfill_keeps_syncing()
    {
        var mailbox = NewGmail();
        mailbox.StartBackfill("h1", "page-1", 10, Now);

        mailbox.RecordSuccess(Now);

        Assert.Equal(MailboxStatus.Syncing, mailbox.Status);
    }

    [Fact]
    public void Needs_reconnect_stops_the_sync_until_reconnected()
    {
        var mailbox = NewGmail();
        mailbox.StartBackfill("h1", "page-1", 10, Now);

        mailbox.MarkNeedsReconnect("invalid credentials");
        Assert.Equal(MailboxStatus.NeedsReconnect, mailbox.Status);
        Assert.False(mailbox.CanSync);

        mailbox.Reconnect();
        Assert.Equal(MailboxStatus.Syncing, mailbox.Status);
        Assert.True(mailbox.CanSync);
        Assert.Null(mailbox.LastError);
    }

    [Fact]
    public void Reconnect_leaves_a_working_mailbox_alone()
    {
        var mailbox = NewGmail();
        mailbox.StartBackfill("h1", null, 0, Now);
        mailbox.RecordFailure("timeout");

        mailbox.Reconnect();

        Assert.Equal(1, mailbox.ConsecutiveFailures);
        Assert.Equal("timeout", mailbox.LastError);
    }
}
