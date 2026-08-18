using Microsoft.Extensions.Options;
using Pottmayer.Pandora.Modules.Channels.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Application.Commands.CreateChannelLink;
using Pottmayer.Pandora.Modules.Channels.Application.Commands.SendTestNotification;
using Pottmayer.Pandora.Modules.Channels.Application.Commands.UnlinkChannel;
using Pottmayer.Pandora.Modules.Channels.Application.Enqueue;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Channels.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Channels.Tests;

public sealed class ChannelLinkingTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FixedTimeProvider _time = new(Now);
    private readonly FakeChannelLinkTokenRepository _tokens = new();
    private readonly FakeUserChannelRepository _channels = new();
    private readonly FakeNotificationRepository _notifications = new();

    private static IOptions<ChannelsOptions> Options(string botUsername = "pandora_bot")
        => Microsoft.Extensions.Options.Options.Create(new ChannelsOptions
        {
            Telegram = new TelegramChannelOptions { BotUsername = botUsername }
        });

    private FakeUnitOfWorkFactory Factory()
    {
        var ctx = new FakeDataContext()
            .Register<IChannelLinkTokenRepository>(_tokens)
            .Register<IUserChannelRepository>(_channels)
            .Register<INotificationRepository>(_notifications);
        return new FakeUnitOfWorkFactory(ctx);
    }

    private UserChannel Linked(Guid userId, string locale = "pt-BR") => UserChannel.LinkVerified(
        userId, Channel.Telegram, NotificationAddress.Create(Channel.Telegram, "123456789"), locale, "{}", _time);

    // ── Issuing the deep link ──

    [Fact]
    public async Task Issues_a_deep_link_carrying_a_single_use_code()
    {
        var handler = new CreateChannelLinkCommandHandler(Factory(), Options(), _time);
        var userId = Guid.NewGuid();

        var result = await handler.Handle(
            new CreateChannelLinkCommand(new CreateChannelLinkInput(userId, "telegram", "pt-BR")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.StartsWith("https://t.me/pandora_bot?start=", result.Value!.Url);
        Assert.Equal(Now + ChannelLinkToken.DefaultLifetime, result.Value.ExpiresAt);

        var issued = Assert.Single(_tokens.Added);
        Assert.Equal(userId, issued.UserId);
        Assert.Equal("pt-BR", issued.Locale);
        Assert.True(issued.IsUsable(Now));
    }

    [Fact]
    public async Task Only_the_hash_is_persisted_never_the_code()
    {
        var handler = new CreateChannelLinkCommandHandler(Factory(), Options(), _time);

        var result = await handler.Handle(
            new CreateChannelLinkCommand(new CreateChannelLinkInput(Guid.NewGuid(), "telegram", "en")),
            CancellationToken.None);

        var code = result.Value!.Url.Split("?start=")[1];
        var stored = Assert.Single(_tokens.Added).TokenHash;

        Assert.NotEqual(code, stored);
        Assert.Equal(64, stored.Length); // sha-256, hex
    }

    [Fact]
    public async Task Refuses_to_link_a_channel_that_has_no_handshake()
    {
        var handler = new CreateChannelLinkCommandHandler(Factory(), Options(), _time);

        var result = await handler.Handle(
            new CreateChannelLinkCommand(new CreateChannelLinkInput(Guid.NewGuid(), "email", "en")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_tokens.Added);
    }

    [Fact]
    public async Task Refuses_to_link_when_no_bot_is_configured()
    {
        var handler = new CreateChannelLinkCommandHandler(Factory(), Options(botUsername: ""), _time);

        var result = await handler.Handle(
            new CreateChannelLinkCommand(new CreateChannelLinkInput(Guid.NewGuid(), "telegram", "en")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_tokens.Added);
    }

    // ── Unlinking ──

    [Fact]
    public async Task Unlink_forgets_the_address()
    {
        var userId = Guid.NewGuid();
        var link = Linked(userId);
        var channels = new FakeUserChannelRepository(link);
        var ctx = new FakeDataContext().Register<IUserChannelRepository>(channels);
        var handler = new UnlinkChannelCommandHandler(new FakeUnitOfWorkFactory(ctx));

        var result = await handler.Handle(
            new UnlinkChannelCommand(new UnlinkChannelInput(userId, "telegram")), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(channels.Items);
    }

    [Fact]
    public async Task Unlink_of_something_never_linked_fails()
    {
        var handler = new UnlinkChannelCommandHandler(Factory());

        var result = await handler.Handle(
            new UnlinkChannelCommand(new UnlinkChannelInput(Guid.NewGuid(), "telegram")), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Unlink_rejects_an_unknown_channel()
    {
        var handler = new UnlinkChannelCommandHandler(Factory());

        var result = await handler.Handle(
            new UnlinkChannelCommand(new UnlinkChannelInput(Guid.NewGuid(), "carrier-pigeon")), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    // ── Test send ──

    [Fact]
    public async Task Test_send_queues_to_the_linked_address_in_its_locale()
    {
        var userId = Guid.NewGuid();
        var channels = new FakeUserChannelRepository(Linked(userId));
        var ctx = new FakeDataContext()
            .Register<IUserChannelRepository>(channels)
            .Register<INotificationRepository>(_notifications);
        var factory = new FakeUnitOfWorkFactory(ctx);
        var enqueuer = new NotificationEnqueuer(factory, new FakeTemplateRenderer(), _time);
        var handler = new SendTestNotificationCommandHandler(factory, enqueuer);

        var result = await handler.Handle(
            new SendTestNotificationCommand(new SendTestNotificationInput(userId, "telegram")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var queued = Assert.Single(_notifications.Added);
        Assert.Equal("telegram", queued.Channel.Value);
        Assert.Equal("123456789", queued.Address.Value);
        Assert.Equal("channel-test", queued.TemplateKey.Value);
        Assert.Equal("pt-BR", queued.Locale);
    }

    [Fact]
    public async Task Test_send_refuses_a_disabled_address()
    {
        var userId = Guid.NewGuid();
        var link = Linked(userId);
        link.Disable("bot was blocked");
        var channels = new FakeUserChannelRepository(link);
        var ctx = new FakeDataContext()
            .Register<IUserChannelRepository>(channels)
            .Register<INotificationRepository>(_notifications);
        var factory = new FakeUnitOfWorkFactory(ctx);
        var handler = new SendTestNotificationCommandHandler(
            factory, new NotificationEnqueuer(factory, new FakeTemplateRenderer(), _time));

        var result = await handler.Handle(
            new SendTestNotificationCommand(new SendTestNotificationInput(userId, "telegram")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_notifications.Added);
    }

    [Fact]
    public async Task Test_send_refuses_a_channel_that_is_not_linked()
    {
        var factory = Factory();
        var handler = new SendTestNotificationCommandHandler(
            factory, new NotificationEnqueuer(factory, new FakeTemplateRenderer(), _time));

        var result = await handler.Handle(
            new SendTestNotificationCommand(new SendTestNotificationInput(Guid.NewGuid(), "telegram")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_notifications.Added);
    }
}
