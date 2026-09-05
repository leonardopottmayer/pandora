using Microsoft.Extensions.Logging.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Channels.Infrastructure.Ingress;
using Pottmayer.Pandora.Modules.Channels.Tests.Fakes;
using Pottmayer.Tars.Communication.Telegram.Abstractions.Models;
using Xunit;

namespace Pottmayer.Pandora.Modules.Channels.Tests;

public sealed class TelegramInboundTriageTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private const string ChatId = "123456789";

    private readonly FixedTimeProvider _time = new(Now);
    private readonly FakeInboundUpdateRepository _updates = new();
    private readonly FakeUserChannelRepository _channels;
    private readonly FakeIntegrationEventBus _bus = new();
    private readonly FakeTelegramClient _client = new();
    private readonly Guid _userId = Guid.NewGuid();

    public TelegramInboundTriageTests()
    {
        var linked = UserChannel.LinkVerified(
            _userId, Channel.Telegram, NotificationAddress.Create(Channel.Telegram, ChatId), "pt-BR", "{}", _time);
        _channels = new FakeUserChannelRepository(linked);
    }

    private TelegramInboundTriage Triage()
    {
        var ctx = new FakeDataContext()
            .Register<IInboundUpdateRepository>(_updates)
            .Register<IUserChannelRepository>(_channels);
        return new TelegramInboundTriage(
            new FakeUnitOfWorkFactory(ctx), _bus, new FakeSender(), new FakeTelegramClientFactory(_client), new FakeChannelsMetrics(), _time,
            NullLogger<TelegramInboundTriage>.Instance);
    }

    private static TelegramUpdate TextUpdate(long updateId, long chatId, string text) =>
        new(updateId, Message: new TelegramIncomingMessage(
            1, new TelegramChat(chatId, "private"), new TelegramSender(chatId), Now, Text: text));

    [Fact]
    public async Task A_message_from_a_linked_user_becomes_an_inbound_message_event()
    {
        await Triage().HandleAsync("assistant", TextUpdate(10, long.Parse(ChatId), "pagar a conta amanhã"), CancellationToken.None);

        var evt = Assert.Single(_bus.Published);
        var message = Assert.IsType<InboundMessageReceived>(evt);
        Assert.Equal(_userId, message.UserId);
        Assert.Equal("telegram", message.Channel);
        Assert.Equal("assistant", message.Bot);
        Assert.Equal("pagar a conta amanhã", message.Text);
        Assert.Null(message.MediaRef);

        var recorded = Assert.Single(_updates.Added);
        Assert.Equal(InboundClassification.Message, recorded.Classification);
        Assert.Equal("assistant", recorded.Bot);
        Assert.Equal(_userId, recorded.UserId);
    }

    [Fact]
    public async Task The_same_update_id_on_a_different_bot_is_not_deduplicated()
    {
        // update_id is unique per bot, so idempotency is keyed by (provider, bot): the notifications bot
        // having seen id 30 must not suppress the assistant bot's own id 30.
        await Triage().HandleAsync("notifications", TextUpdate(30, long.Parse(ChatId), "primeira"), CancellationToken.None);
        await Triage().HandleAsync("assistant", TextUpdate(30, long.Parse(ChatId), "segunda"), CancellationToken.None);

        Assert.Equal(2, _bus.Published.Count);
        Assert.Equal(2, _updates.Added.Count);
    }

    [Fact]
    public async Task A_message_from_an_unknown_chat_is_discarded_with_a_reply()
    {
        await Triage().HandleAsync("notifications", TextUpdate(11, 999_999, "olá"), CancellationToken.None);

        Assert.Empty(_bus.Published);
        var reply = Assert.Single(_client.Sent);
        Assert.Equal("999999", reply.ChatId);
        Assert.Equal(InboundClassification.Discarded, Assert.Single(_updates.Added).Classification);
    }

    [Fact]
    public async Task An_already_seen_update_is_a_no_op()
    {
        var seen = new FakeInboundUpdateRepository(
            InboundUpdate.Record("telegram", "notifications", 12, "{}", _userId, InboundClassification.Message, _time));
        var ctx = new FakeDataContext()
            .Register<IInboundUpdateRepository>(seen)
            .Register<IUserChannelRepository>(_channels);
        var triage = new TelegramInboundTriage(
            new FakeUnitOfWorkFactory(ctx), _bus, new FakeSender(), new FakeTelegramClientFactory(_client), new FakeChannelsMetrics(), _time,
            NullLogger<TelegramInboundTriage>.Instance);

        await triage.HandleAsync("notifications", TextUpdate(12, long.Parse(ChatId), "duplicada"), CancellationToken.None);

        Assert.Empty(_bus.Published);
        Assert.Empty(_client.Sent);
        Assert.Empty(seen.Added);
    }

    [Fact]
    public async Task A_callback_query_is_acknowledged_and_not_routed()
    {
        var update = new TelegramUpdate(13, CallbackQuery: new TelegramCallbackQuery(
            "cb-1", new TelegramSender(long.Parse(ChatId)), Data: "anything"));

        await Triage().HandleAsync("notifications", update, CancellationToken.None);

        Assert.Equal("cb-1", Assert.Single(_client.AnsweredCallbacks));
        Assert.Empty(_bus.Published);
        Assert.Equal(InboundClassification.Interaction, Assert.Single(_updates.Added).Classification);
    }
}
