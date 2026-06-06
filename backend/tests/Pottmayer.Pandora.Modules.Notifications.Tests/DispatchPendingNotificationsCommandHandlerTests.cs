using Pottmayer.Pandora.Modules.Notifications.Application.Commands.DispatchPending;
using Pottmayer.Pandora.Modules.Notifications.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notifications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Notifications.Tests.Fakes;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notifications.Tests;

public sealed class DispatchPendingNotificationsCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FixedTimeProvider _time = new(Now);

    private (DispatchPendingNotificationsCommandHandler Handler, FakeNotificationRepository Repo, FakeEmailSender Email) Build(
        FakeEmailSender email, params Notification[] seed)
    {
        var repo = new FakeNotificationRepository(seed);
        var ctx = new FakeDataContext().Register<INotificationRepository>(repo);
        var handler = new DispatchPendingNotificationsCommandHandler(new FakeUnitOfWorkFactory(ctx), email, _time);
        return (handler, repo, email);
    }

    private Notification Pending(int maxAttempts = Notification.DefaultMaxAttempts)
        => Notification.Queue(
            Channel.Email, Email.Create("alice@example.com"), TemplateKey.Create("account-activation"),
            "en", "{}", new NotificationContent("Subject", "Body", false), Guid.NewGuid(), _time, maxAttempts);

    private static DispatchPendingNotificationsCommand Command(int batchSize = 20)
        => new(new DispatchPendingNotificationsInput(batchSize));

    [Fact]
    public async Task Sends_due_notifications_and_marks_them_sent()
    {
        var email = new FakeEmailSender { Provider = "ses", ProviderMessageId = "msg-42" };
        var n = Pending();
        var (handler, repo, _) = Build(email, n);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Sent);
        Assert.Equal(0, result.Value.Failed);
        Assert.Equal(0, result.Value.Dead);

        Assert.Equal(NotificationStatus.Sent, n.Status);
        Assert.Equal("ses", n.Provider);
        Assert.Equal("msg-42", n.ProviderMessageId);
        Assert.Same(n, Assert.Single(repo.Updated));

        var message = Assert.Single(email.Sent);
        Assert.Equal(["alice@example.com"], message.To);
        Assert.Equal("Subject", message.Subject);
        Assert.Equal("Body", message.Body);
    }

    [Fact]
    public async Task Counts_a_failed_attempt_and_reschedules()
    {
        var email = new FakeEmailSender { Throw = new InvalidOperationException("smtp down") };
        var n = Pending(maxAttempts: 5);
        var (handler, _, _) = Build(email, n);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(0, result.Value!.Sent);
        Assert.Equal(1, result.Value.Failed);
        Assert.Equal(0, result.Value.Dead);
        Assert.Equal(NotificationStatus.Failed, n.Status);
        Assert.Equal("smtp down", n.LastError);
    }

    [Fact]
    public async Task Counts_a_dead_letter_when_attempts_are_exhausted()
    {
        var email = new FakeEmailSender { Throw = new InvalidOperationException("smtp down") };
        var n = Pending(maxAttempts: 1);
        var (handler, _, _) = Build(email, n);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(0, result.Value!.Sent);
        Assert.Equal(0, result.Value.Failed);
        Assert.Equal(1, result.Value.Dead);
        Assert.Equal(NotificationStatus.Dead, n.Status);
    }

    [Fact]
    public async Task Skips_notifications_that_are_not_due()
    {
        var email = new FakeEmailSender();
        var n = Pending();
        n.MarkSending();
        n.MarkSent("p", "id"); // terminal -> not due
        var (handler, _, _) = Build(email, n);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(0, result.Value!.Total);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task Honors_the_batch_size()
    {
        var email = new FakeEmailSender();
        var (handler, _, _) = Build(email, Pending(), Pending(), Pending());

        var result = await handler.Handle(Command(batchSize: 2), CancellationToken.None);

        Assert.Equal(2, result.Value!.Sent);
        Assert.Equal(2, email.Sent.Count);
    }
}
