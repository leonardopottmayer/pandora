using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Channels.Infrastructure.Transports;
using Pottmayer.Pandora.Modules.Channels.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Channels.Tests;

public sealed class EmailChannelTransportTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Notification Queued() => Notification.Queue(
        Channel.Email,
        NotificationAddress.Create(Channel.Email, "alice@example.com"),
        TemplateKey.Create("account-activation"),
        "en",
        "{}",
        new NotificationContent("Subject", "Body", IsHtml: true),
        Guid.NewGuid(),
        new FixedTimeProvider(Now));

    [Fact]
    public async Task Maps_the_queued_row_onto_an_email_message()
    {
        var sender = new FakeEmailSender { Provider = "ses", ProviderMessageId = "msg-42" };
        var transport = new EmailChannelTransport(sender);

        var result = await transport.SendAsync(Queued());

        var message = Assert.Single(sender.Sent);
        Assert.Equal(["alice@example.com"], message.To);
        Assert.Equal("Subject", message.Subject);
        Assert.Equal("Body", message.Body);
        Assert.True(message.IsHtml);

        Assert.Equal("ses", result.Provider);
        Assert.Equal("msg-42", result.ProviderMessageId);
    }

    [Fact]
    public async Task Serves_the_email_channel()
    {
        Assert.Equal(Channel.Email, new EmailChannelTransport(new FakeEmailSender()).Channel);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task A_provider_failure_propagates_as_transient()
    {
        var sender = new FakeEmailSender { Throw = new InvalidOperationException("smtp down") };
        var transport = new EmailChannelTransport(sender);

        // Not a PermanentDeliveryException: SMTP does not tell us reliably that an address is gone.
        await Assert.ThrowsAsync<InvalidOperationException>(() => transport.SendAsync(Queued()));
    }
}
