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

    private readonly FakeInteractionRepository _interactions = new();

    private SendAssistantReplyHandler Handler(FakeTelegramSender sender, params UserChannel[] channels)
    {
        var ctx = new FakeDataContext()
            .Register<IUserChannelRepository>(new FakeUserChannelRepository(channels))
            .Register<IInteractionRepository>(_interactions);
        return new SendAssistantReplyHandler(
            new FakeUnitOfWorkFactory(ctx), sender, _time, NullLogger<SendAssistantReplyHandler>.Instance);
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

    [Fact]
    public async Task Buttons_are_registered_as_interactions_and_sent_with_their_ids()
    {
        var userId = Guid.NewGuid();
        var sender = new FakeTelegramSender();
        var invocationId = Guid.NewGuid().ToString();

        await Handler(sender, Linked(userId, "123")).HandleAsync(new SendAssistantReply(
            Guid.NewGuid(), Now, userId, "assistant", "Criar o lembrete?",
            [
                new NotificationButton("assistant", "confirm", "✅ Confirmar", invocationId),
                new NotificationButton("assistant", "cancel", "❌ Cancelar", invocationId),
            ]));

        Assert.Equal(2, _interactions.Added.Count);
        var confirm = _interactions.Added[0];
        Assert.Equal((userId, "assistant", "confirm", invocationId), (confirm.UserId, confirm.OwnerModule, confirm.Action, confirm.Payload));
        Assert.Null(confirm.NotificationId);
        Assert.Equal(Now + Interaction.Lifetime, confirm.ExpiresAt);

        var buttons = Assert.Single(sender.Sent).Buttons!;
        Assert.Equal(["✅ Confirmar", "❌ Cancelar"], buttons.Select(b => b.Label));
        Assert.Equal(_interactions.Added.Select(i => i.Id.ToString()), buttons.Select(b => b.InteractionId));
    }

    [Fact]
    public async Task A_reply_over_telegrams_limit_goes_out_in_pieces_cut_at_line_breaks_with_the_buttons_on_the_last()
    {
        var userId = Guid.NewGuid();
        var sender = new FakeTelegramSender();
        var line = new string('a', 1000);
        var text = string.Join("\n", Enumerable.Repeat(line, 9)); // four lines fit in one message (4003 chars)

        await Handler(sender, Linked(userId, "123")).HandleAsync(new SendAssistantReply(
            Guid.NewGuid(), Now, userId, "assistant", text,
            [new NotificationButton("assistant", "confirm", "✅ Confirmar", "x")]));

        Assert.Equal([4003, 4003, 1000], sender.Sent.Select(s => s.Text.Length));
        Assert.Equal([0, 0, 1], sender.Sent.Select(s => s.Buttons?.Count ?? 0));
        Assert.Equal(text, string.Join("\n", sender.Sent.Select(s => s.Text)));
    }

    [Fact]
    public void A_single_line_longer_than_the_limit_is_cut_where_it_must()
    {
        var parts = SendAssistantReplyHandler.Split(new string('a', 10), 4);

        Assert.Equal(["aaaa", "aaaa", "aa"], parts);
    }
}
