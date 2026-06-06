using Pottmayer.Pandora.Modules.Notifications.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Notifications.Tests.Fakes;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notifications.Tests;

public sealed class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Notification Queue(TimeProvider time, int maxAttempts = Notification.DefaultMaxAttempts)
        => Notification.Queue(
            Channel.Email,
            Email.Create("alice@example.com"),
            TemplateKey.Create("account-activation"),
            "en",
            payload: "{}",
            new NotificationContent("Subject", "Body", IsHtml: true),
            correlationId: Guid.NewGuid(),
            time,
            maxAttempts);

    [Fact]
    public void Queue_starts_pending_due_now_with_content_copied()
    {
        var time = new FixedTimeProvider(Now);

        var n = Queue(time);

        Assert.NotEqual(Guid.Empty, n.Id);
        Assert.Equal(NotificationStatus.Pending, n.Status);
        Assert.Equal(0, n.AttemptCount);
        Assert.Equal(Notification.DefaultMaxAttempts, n.MaxAttempts);
        Assert.Equal(Now, n.NextAttemptAt);
        Assert.Equal(Now, n.CreatedAt);
        Assert.Equal("Subject", n.Subject);
        Assert.Equal("Body", n.Body);
        Assert.True(n.IsHtml);
        Assert.True(n.IsDue(Now));
    }

    [Fact]
    public void IsDue_is_false_before_NextAttemptAt()
    {
        var n = Queue(new FixedTimeProvider(Now));

        Assert.False(n.IsDue(Now - TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void IsDue_is_false_for_terminal_and_sending_states()
    {
        var time = new FixedTimeProvider(Now);

        var sent = Queue(time);
        sent.MarkSending();
        Assert.False(sent.IsDue(Now)); // Sending

        sent.MarkSent("p", "id");
        Assert.False(sent.IsDue(Now)); // Sent

        var dead = Queue(time, maxAttempts: 1);
        dead.MarkFailed("boom", time);
        Assert.Equal(NotificationStatus.Dead, dead.Status);
        Assert.False(dead.IsDue(Now)); // Dead
    }

    [Fact]
    public void MarkSent_records_provider_and_clears_error()
    {
        var time = new FixedTimeProvider(Now);
        var n = Queue(time);
        n.MarkFailed("earlier failure", time); // leaves LastError set
        n.MarkSending();

        n.MarkSent("ses", "provider-msg-7");

        Assert.Equal(NotificationStatus.Sent, n.Status);
        Assert.Equal("ses", n.Provider);
        Assert.Equal("provider-msg-7", n.ProviderMessageId);
        Assert.Null(n.LastError);
    }

    [Fact]
    public void MarkSending_throws_when_terminal()
    {
        var time = new FixedTimeProvider(Now);
        var n = Queue(time);
        n.MarkSending();
        n.MarkSent("p", "id");

        Assert.Throws<InvalidOperationException>(n.MarkSending);
    }

    [Fact]
    public void MarkFailed_reschedules_with_exponential_backoff()
    {
        var time = new FixedTimeProvider(Now);
        var n = Queue(time, maxAttempts: 10);

        n.MarkFailed("boom", time);
        Assert.Equal(NotificationStatus.Failed, n.Status);
        Assert.Equal(1, n.AttemptCount);
        Assert.Equal("boom", n.LastError);
        Assert.Equal(Now + TimeSpan.FromMinutes(1), n.NextAttemptAt); // 2^0

        n.MarkFailed("boom", time);
        Assert.Equal(Now + TimeSpan.FromMinutes(2), n.NextAttemptAt); // 2^1

        n.MarkFailed("boom", time);
        Assert.Equal(Now + TimeSpan.FromMinutes(4), n.NextAttemptAt); // 2^2
    }

    [Fact]
    public void MarkFailed_caps_backoff_at_one_hour()
    {
        var time = new FixedTimeProvider(Now);
        var n = Queue(time, maxAttempts: 20);

        // 2^(attempt-1) minutes exceeds 60 from the 8th attempt on (2^7 = 128).
        for (var i = 0; i < 10; i++)
            n.MarkFailed("boom", time);

        Assert.Equal(Now + TimeSpan.FromMinutes(60), n.NextAttemptAt);
    }

    [Fact]
    public void MarkFailed_moves_to_dead_once_attempts_exhausted()
    {
        var time = new FixedTimeProvider(Now);
        var n = Queue(time, maxAttempts: 2);

        n.MarkFailed("first", time);
        Assert.Equal(NotificationStatus.Failed, n.Status);

        n.MarkFailed("second", time);
        Assert.Equal(NotificationStatus.Dead, n.Status);
        Assert.Equal(2, n.AttemptCount);
    }

    [Fact]
    public void MarkDead_is_terminal_and_records_error()
    {
        var n = Queue(new FixedTimeProvider(Now));

        n.MarkDead("permanent failure");

        Assert.Equal(NotificationStatus.Dead, n.Status);
        Assert.Equal("permanent failure", n.LastError);
    }

    [Fact]
    public void Errors_longer_than_the_limit_are_truncated()
    {
        var time = new FixedTimeProvider(Now);
        var n = Queue(time, maxAttempts: 10);
        var huge = new string('x', 5000);

        n.MarkFailed(huge, time);

        Assert.Equal(1000, n.LastError!.Length);
    }
}
