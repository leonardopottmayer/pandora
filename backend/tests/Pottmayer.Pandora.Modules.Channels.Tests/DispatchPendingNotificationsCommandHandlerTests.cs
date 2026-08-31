using Pottmayer.Pandora.Modules.Channels.Application.Commands.DispatchPending;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Channels.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Channels.Tests;

public sealed class DispatchPendingNotificationsCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private const string Recipient = "alice@example.com";

    private readonly FixedTimeProvider _time = new(Now);
    private readonly FakeIntegrationEventBus _bus = new();

    private (DispatchPendingNotificationsCommandHandler Handler, FakeNotificationRepository Repo, FakeUserChannelRepository Links) Build(
        IChannelTransport transport, FakeUserChannelRepository links = null!, params Notification[] seed)
    {
        var repo = new FakeNotificationRepository(seed);
        links ??= new FakeUserChannelRepository();
        var ctx = new FakeDataContext()
            .Register<INotificationRepository>(repo)
            .Register<IUserChannelRepository>(links);
        var handler = new DispatchPendingNotificationsCommandHandler(
            new FakeUnitOfWorkFactory(ctx), [transport], _bus, new FakeChannelsMetrics(), _time);
        return (handler, repo, links);
    }

    private Notification Pending(Channel? channel = null, int maxAttempts = Notification.DefaultMaxAttempts)
    {
        channel ??= Channel.Email;
        var address = channel == Channel.Email
            ? NotificationAddress.Create(Channel.Email, Recipient)
            : NotificationAddress.Create(Channel.Telegram, "123456789");

        return Notification.Queue(
            channel, address, TemplateKey.Create("account-activation"),
            "en", "{}", new NotificationContent("Subject", "Body", false), Guid.NewGuid(), _time,
            maxAttempts: maxAttempts);
    }

    private UserChannel LinkedEmail(Guid? userId = null) => UserChannel.LinkVerified(
        userId ?? Guid.NewGuid(), Channel.Email, NotificationAddress.Create(Channel.Email, Recipient),
        "en", "{}", _time);

    private static DispatchPendingNotificationsCommand Command(int batchSize = 20)
        => new(new DispatchPendingNotificationsInput(batchSize));

    [Fact]
    public async Task Sends_due_notifications_and_marks_them_sent()
    {
        var transport = new FakeChannelTransport(Channel.Email) { Provider = "ses", ProviderMessageId = "msg-42" };
        var n = Pending();
        var (handler, repo, _) = Build(transport, seed: n);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Sent);
        Assert.Equal(0, result.Value.Failed);
        Assert.Equal(0, result.Value.Dead);

        Assert.Equal(NotificationStatus.Sent, n.Status);
        Assert.Equal("ses", n.Provider);
        Assert.Equal("msg-42", n.ProviderMessageId);
        Assert.Same(n, Assert.Single(repo.Updated));
        Assert.Same(n, Assert.Single(transport.Sent));
    }

    [Fact]
    public async Task Picks_the_transport_that_serves_the_channel()
    {
        var email = new FakeChannelTransport(Channel.Email);
        var telegram = new FakeChannelTransport(Channel.Telegram);
        var repo = new FakeNotificationRepository(Pending(Channel.Telegram));
        var ctx = new FakeDataContext()
            .Register<INotificationRepository>(repo)
            .Register<IUserChannelRepository>(new FakeUserChannelRepository());
        var handler = new DispatchPendingNotificationsCommandHandler(
            new FakeUnitOfWorkFactory(ctx), [email, telegram], _bus, new FakeChannelsMetrics(), _time);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(1, result.Value!.Sent);
        Assert.Empty(email.Sent);
        Assert.Single(telegram.Sent);
    }

    [Fact]
    public async Task A_channel_with_no_transport_dies_instead_of_retrying_forever()
    {
        var n = Pending(Channel.Telegram);
        var (handler, _, _) = Build(new FakeChannelTransport(Channel.Email), seed: n);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(1, result.Value!.Dead);
        Assert.Equal(NotificationStatus.Dead, n.Status);
        Assert.Contains("telegram", n.LastError);
    }

    [Fact]
    public async Task Counts_a_failed_attempt_and_reschedules()
    {
        var transport = new FakeChannelTransport(Channel.Email) { Throw = new InvalidOperationException("smtp down") };
        var n = Pending(maxAttempts: 5);
        var (handler, _, _) = Build(transport, seed: n);

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
        var transport = new FakeChannelTransport(Channel.Email) { Throw = new InvalidOperationException("smtp down") };
        var n = Pending(maxAttempts: 1);
        var (handler, _, _) = Build(transport, seed: n);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(0, result.Value!.Sent);
        Assert.Equal(0, result.Value.Failed);
        Assert.Equal(1, result.Value.Dead);
        Assert.Equal(NotificationStatus.Dead, n.Status);
    }

    [Fact]
    public async Task A_permanent_failure_kills_the_row_on_the_first_attempt()
    {
        var transport = new FakeChannelTransport(Channel.Email)
        {
            Throw = new PermanentDeliveryException("bot was blocked by the user")
        };
        var n = Pending(maxAttempts: 5);
        var (handler, _, _) = Build(transport, seed: n);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(1, result.Value!.Dead);
        Assert.Equal(NotificationStatus.Dead, n.Status);
        Assert.Equal(0, n.AttemptCount); // no backoff was spent
        Assert.Equal("bot was blocked by the user", n.LastError);
    }

    [Fact]
    public async Task A_permanent_failure_disables_the_address_and_announces_it()
    {
        var transport = new FakeChannelTransport(Channel.Email)
        {
            Throw = new PermanentDeliveryException("chat not found")
        };
        var userId = Guid.NewGuid();
        var link = LinkedEmail(userId);
        var (handler, _, links) = Build(transport, new FakeUserChannelRepository(link), Pending());

        await handler.Handle(Command(), CancellationToken.None);

        Assert.False(link.IsEnabled);
        Assert.Equal("chat not found", link.DisabledReason);
        Assert.Same(link, Assert.Single(links.Updated));

        var published = Assert.Single(_bus.Published);
        var evt = Assert.IsType<UserChannelDisabled>(published);
        Assert.Equal(userId, evt.UserId);
        Assert.Equal("email", evt.Channel);
        Assert.Equal("chat not found", evt.Reason);
    }

    [Fact]
    public async Task A_transient_failure_leaves_the_address_alone()
    {
        var transport = new FakeChannelTransport(Channel.Email) { Throw = new InvalidOperationException("smtp down") };
        var link = LinkedEmail();
        var (handler, _, links) = Build(transport, new FakeUserChannelRepository(link), Pending());

        await handler.Handle(Command(), CancellationToken.None);

        Assert.True(link.IsEnabled);
        Assert.Empty(links.Updated);
        Assert.Empty(_bus.Published);
    }

    [Fact]
    public async Task An_address_already_disabled_is_not_announced_twice()
    {
        var transport = new FakeChannelTransport(Channel.Email)
        {
            Throw = new PermanentDeliveryException("chat not found")
        };
        var link = LinkedEmail();
        link.Disable("earlier failure");
        var (handler, _, links) = Build(transport, new FakeUserChannelRepository(link), Pending());

        await handler.Handle(Command(), CancellationToken.None);

        Assert.Empty(links.Updated);
        Assert.Empty(_bus.Published);
        Assert.Equal("earlier failure", link.DisabledReason);
    }

    [Fact]
    public async Task Skips_notifications_that_are_not_due()
    {
        var transport = new FakeChannelTransport(Channel.Email);
        var n = Pending();
        n.MarkSending();
        n.MarkSent("p", "id"); // terminal -> not due
        var (handler, _, _) = Build(transport, seed: n);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(0, result.Value!.Total);
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task Honors_the_batch_size()
    {
        var transport = new FakeChannelTransport(Channel.Email);
        var (handler, _, _) = Build(transport, null!, Pending(), Pending(), Pending());

        var result = await handler.Handle(Command(batchSize: 2), CancellationToken.None);

        Assert.Equal(2, result.Value!.Sent);
        Assert.Equal(2, transport.Sent.Count);
    }
}
