using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Channels.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IChannelTransport"/> for one channel. Succeeds by default; set
/// <see cref="Throw"/> to simulate a provider failure, permanent or not.
/// </summary>
internal sealed class FakeChannelTransport(Channel channel) : IChannelTransport
{
    public Channel Channel { get; } = channel;

    public List<Notification> Sent { get; } = [];
    public Exception? Throw { get; set; }
    public string Provider { get; set; } = "fake";
    public string? ProviderMessageId { get; set; } = "msg-1";

    public Task<ChannelDeliveryResult> SendAsync(Notification notification, CancellationToken ct = default)
    {
        Sent.Add(notification);
        if (Throw is not null)
            throw Throw;
        return Task.FromResult(new ChannelDeliveryResult(Provider, ProviderMessageId));
    }
}
