using Pottmayer.Pandora.Modules.Channels.Application.Enqueue;
using Pottmayer.Pandora.Modules.Channels.Application.Subscribers;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Channels.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Channels.Tests;

public sealed class NotifyUserRequestedHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid User = Guid.NewGuid();
    private const string Category = "agenda.reminder";

    private static UserChannel Linked(Channel channel, string address) =>
        UserChannel.LinkVerified(
            User, channel, NotificationAddress.Create(channel, address), "pt-BR", "{}", new FixedTimeProvider(Now));

    private static NotificationPreference Preference(params Channel[] channels) =>
        NotificationPreference.Create(User, Category, channels, new FixedTimeProvider(Now));

    private static (NotifyUserRequestedHandler Handler, FakeNotificationRepository Notifications) Build(
        IEnumerable<UserChannel> channels, IEnumerable<NotificationPreference> preferences)
    {
        var notifications = new FakeNotificationRepository();
        var ctx = new FakeDataContext()
            .Register<INotificationRepository>(notifications)
            .Register<IUserChannelRepository>(new FakeUserChannelRepository([.. channels]))
            .Register<INotificationPreferenceRepository>(new FakeNotificationPreferenceRepository([.. preferences]));

        var factory = new FakeUnitOfWorkFactory(ctx);
        var time = new FixedTimeProvider(Now);
        var enqueuer = new NotificationEnqueuer(factory, new FakeTemplateRenderer(), time);
        return (new NotifyUserRequestedHandler(factory, enqueuer, time), notifications);
    }

    private static NotifyUserRequested Event(IReadOnlyList<string>? channels = null) =>
        new(Guid.NewGuid(), Now, User, Category, "agenda.reminder.due", Locale: null,
            Channels: channels, new Dictionary<string, string>(), Guid.NewGuid());

    [Fact]
    public async Task Fans_out_to_every_preferred_channel_under_one_group()
    {
        var (handler, notifications) = Build(
            [Linked(Channel.Email, "alice@example.com"), Linked(Channel.Telegram, "123456789")],
            [Preference(Channel.Email, Channel.Telegram)]);

        await handler.HandleAsync(Event());

        Assert.Equal(2, notifications.Added.Count);
        Assert.Contains(notifications.Added, n => n.Channel == Channel.Email);
        Assert.Contains(notifications.Added, n => n.Channel == Channel.Telegram);
        // One group across the fan-out; the rows are otherwise independent.
        Assert.NotNull(notifications.Added[0].GroupId);
        Assert.Single(notifications.Added.Select(n => n.GroupId).Distinct());
    }

    [Fact]
    public async Task Sends_only_the_preferred_channel()
    {
        var (handler, notifications) = Build(
            [Linked(Channel.Email, "alice@example.com"), Linked(Channel.Telegram, "123456789")],
            [Preference(Channel.Telegram)]);

        await handler.HandleAsync(Event());

        var n = Assert.Single(notifications.Added);
        Assert.Equal(Channel.Telegram, n.Channel);
    }

    [Fact]
    public async Task Without_a_preference_defaults_to_every_usable_channel()
    {
        var (handler, notifications) = Build(
            [Linked(Channel.Email, "alice@example.com"), Linked(Channel.Telegram, "123456789")],
            []);

        await handler.HandleAsync(Event());

        Assert.Equal(2, notifications.Added.Count);
    }

    [Fact]
    public async Task An_empty_preference_mutes_the_category()
    {
        var (handler, notifications) = Build([Linked(Channel.Email, "alice@example.com")], [Preference()]);

        await handler.HandleAsync(Event());

        Assert.Empty(notifications.Added);
    }

    [Fact]
    public async Task A_preferred_channel_without_a_usable_address_is_dropped()
    {
        var (handler, notifications) = Build(
            [Linked(Channel.Email, "alice@example.com")], // no Telegram linked
            [Preference(Channel.Email, Channel.Telegram)]);

        await handler.HandleAsync(Event());

        var n = Assert.Single(notifications.Added);
        Assert.Equal(Channel.Email, n.Channel);
    }

    [Fact]
    public async Task An_explicit_channel_override_wins_over_the_preference()
    {
        var (handler, notifications) = Build(
            [Linked(Channel.Email, "alice@example.com"), Linked(Channel.Telegram, "123456789")],
            [Preference(Channel.Email)]);

        await handler.HandleAsync(Event(channels: ["telegram"]));

        var n = Assert.Single(notifications.Added);
        Assert.Equal(Channel.Telegram, n.Channel);
    }
}
