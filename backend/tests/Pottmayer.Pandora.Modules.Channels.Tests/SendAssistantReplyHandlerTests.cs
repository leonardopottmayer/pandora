using Pottmayer.Pandora.Modules.Channels.Application.Subscribers;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Channels.Tests.Fakes;
using Pottmayer.Tars.Communication.Telegram.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.Modules.Channels.Tests;

public sealed class SendAssistantReplyHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly FixedTimeProvider _time = new(Now);

    private static SendAssistantReplyHandler Handler(FakeTelegramSender sender, params UserChannel[] channels)
    {
        var ctx = new FakeDataContext().Register<IUserChannelRepository>(new FakeUserChannelRepository(channels));
        return new SendAssistantReplyHandler(
            new FakeUnitOfWorkFactory(ctx), sender, NullLogger<SendAssistantReplyHandler>.Instance);
    }

    private UserChannel Linked(Guid userId, string chatId) => UserChannel.LinkVerified(
        userId, Channel.Telegram, NotificationAddress.Create(Channel.Telegram, chatId), "pt-BR", "{}", _time);

    [Fact]
    public async Task Sends_the_reply_to_the_users_chat_through_the_named_bot()
    {
        var userId = Guid.NewGuid();
        var sender = new FakeTelegramSender();

        await Handler(sender, Linked(userId, "123456789"))
            .HandleAsync(new SendAssistantReply(Guid.NewGuid(), Now, userId, "assistant", "feito ✓"));

        var sent = Assert.Single(sender.Sent);
        Assert.Equal("assistant", sent.Bot);
        Assert.Equal("123456789", sent.ChatId);
        Assert.Equal("feito ✓", sent.Text);
    }

    [Fact]
    public async Task Drops_the_reply_when_the_user_has_no_linked_telegram_chat()
    {
        var sender = new FakeTelegramSender();

        await Handler(sender)
            .HandleAsync(new SendAssistantReply(Guid.NewGuid(), Now, Guid.NewGuid(), "assistant", "oi"));

        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task A_send_failure_is_swallowed_so_the_event_is_not_redelivered()
    {
        var userId = Guid.NewGuid();
        var sender = new FakeTelegramSender
        {
            Throw = new TelegramException("sendMessage", "boom", isPermanent: false),
        };

        // Must not throw — a failed interactive reply is logged, never retried.
        await Handler(sender, Linked(userId, "123"))
            .HandleAsync(new SendAssistantReply(Guid.NewGuid(), Now, userId, "assistant", "oi"));

        Assert.Empty(sender.Sent);
    }
}
