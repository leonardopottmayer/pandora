namespace Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;

/// <summary>
/// The telemetry the module reports about its own operation. A driven port, like
/// <see cref="IChannelTransport"/>: the application code records facts through it and never learns
/// which backend (a .NET <c>Meter</c>, OpenTelemetry, a test double) is on the other side. The
/// implementation lives in Infrastructure so the diagnostics dependency stays out of the use cases.
/// </summary>
public interface IChannelsMetrics
{
    /// <summary>Records one notification reaching a terminal outcome (<c>sent</c>/<c>failed</c>/<c>dead</c>) on a channel.</summary>
    void RecordDispatched(string channel, string outcome);

    /// <summary>Records how long a single transport hand-off took, in milliseconds.</summary>
    void RecordDispatchDuration(string channel, double milliseconds);

    /// <summary>Records one inbound update discarded during triage.</summary>
    void RecordInboundDiscarded();

    /// <summary>Publishes the latest queue depth (notifications not yet in a terminal state).</summary>
    void SetQueueDepth(long depth);
}
