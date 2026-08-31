using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;

namespace Pottmayer.Pandora.Modules.Channels.Tests.Fakes;

/// <summary>No-op <see cref="IChannelsMetrics"/> for tests that don't assert on telemetry.</summary>
internal sealed class FakeChannelsMetrics : IChannelsMetrics
{
    public void RecordDispatched(string channel, string outcome) { }
    public void RecordDispatchDuration(string channel, double milliseconds) { }
    public void RecordInboundDiscarded() { }
    public void SetQueueDepth(long depth) { }
}
