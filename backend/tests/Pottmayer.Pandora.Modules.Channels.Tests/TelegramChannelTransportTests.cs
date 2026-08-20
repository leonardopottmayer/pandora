using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Channels.Infrastructure.Transports;
using Pottmayer.Pandora.Modules.Channels.Tests.Fakes;
using Pottmayer.Tars.Communication.Telegram.Abstractions;
using Xunit;

namespace Pottmayer.Pandora.Modules.Channels.Tests;

public sealed class TelegramChannelTransportTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Notification Queued() => Notification.Queue(
        Channel.Telegram,
        NotificationAddress.Create(Channel.Telegram, "123456789"),
        TemplateKey.Create("channel-test"),
        "pt-BR",
        "{}",
        new NotificationContent(Subject: string.Empty, Body: "Olá do Pandora", IsHtml: false),
        Guid.NewGuid(),
        new FixedTimeProvider(Now));

    [Fact]
    public async Task Sends_the_body_to_the_chat_id_and_returns_the_message_id()
    {
        var client = new FakeTelegramClient { MessageId = 4242 };
        var transport = new TelegramChannelTransport(client);

        var result = await transport.SendAsync(Queued());

        var message = Assert.Single(client.Sent);
        Assert.Equal("123456789", message.ChatId);
        Assert.Equal("Olá do Pandora", message.Text);

        Assert.Equal("telegram", result.Provider);
        Assert.Equal("4242", result.ProviderMessageId);
    }

    [Fact]
    public async Task Serves_the_telegram_channel()
    {
        Assert.Equal(Channel.Telegram, new TelegramChannelTransport(new FakeTelegramClient()).Channel);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task A_permanent_bot_api_failure_becomes_a_permanent_delivery_exception()
    {
        // chat not found / bot blocked: retrying can never help, so the dispatcher must dead-letter
        // and disable the channel rather than burn attempts.
        var client = new FakeTelegramClient
        {
            Throw = new TelegramException("sendMessage", "bot was blocked by the user", isPermanent: true, errorCode: 403),
        };
        var transport = new TelegramChannelTransport(client);

        await Assert.ThrowsAsync<PermanentDeliveryException>(() => transport.SendAsync(Queued()));
    }

    [Fact]
    public async Task A_transient_bot_api_failure_propagates_for_retry()
    {
        // 429/5xx: worth retrying with backoff, so it must not be swallowed as permanent.
        var client = new FakeTelegramClient
        {
            Throw = new TelegramException("sendMessage", "Too Many Requests", isPermanent: false, errorCode: 429),
        };
        var transport = new TelegramChannelTransport(client);

        await Assert.ThrowsAsync<TelegramException>(() => transport.SendAsync(Queued()));
    }
}
