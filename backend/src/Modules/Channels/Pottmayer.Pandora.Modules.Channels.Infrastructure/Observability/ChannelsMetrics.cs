using System.Diagnostics.Metrics;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Services;

namespace Pottmayer.Pandora.Modules.Channels.Infrastructure.Observability;

/// <summary>
/// The <see cref="IChannelsMetrics"/> implementation backed by a .NET <see cref="Meter"/>. One
/// singleton owns the instruments so the dispatcher and the inbound triage report into the same
/// place. The meter name is subscribed by the host's OpenTelemetry wiring (a
/// <c>Pottmayer.Pandora.*</c> wildcard), so nothing here talks to an exporter directly.
/// </summary>
public sealed class ChannelsMetrics : IChannelsMetrics, IDisposable
{
    /// <summary>Meter name; must match the host's <c>AddMeter</c> subscription.</summary>
    public const string MeterName = "Pottmayer.Pandora.Modules.Channels";

    private readonly Meter _meter;
    private readonly Counter<long> _dispatched;
    private readonly Histogram<double> _dispatchDuration;
    private readonly Counter<long> _inboundDiscarded;

    // Last observed queue depth, refreshed by the dispatcher each cycle and read by the gauge on
    // collection. A long is written and read atomically on every platform .NET targets.
    private long _queueDepth;

    public ChannelsMetrics()
    {
        _meter = new Meter(MeterName);

        _dispatched = _meter.CreateCounter<long>(
            "channels.notifications.dispatched",
            unit: "{notification}",
            description: "Notifications leaving the queue, tagged by channel and terminal outcome (sent/failed/dead).");

        _dispatchDuration = _meter.CreateHistogram<double>(
            "channels.notifications.dispatch.duration",
            unit: "ms",
            description: "Time spent handing one notification to its transport.");

        _inboundDiscarded = _meter.CreateCounter<long>(
            "channels.inbound.updates.discarded",
            unit: "{update}",
            description: "Inbound updates classified as Discarded (unknown chat, empty update, unroutable).");

        _meter.CreateObservableGauge(
            "channels.notifications.queue.depth",
            () => Interlocked.Read(ref _queueDepth),
            unit: "{notification}",
            description: "Notifications not yet in a terminal state (pending, failed-awaiting-retry, sending).");
    }

    public void RecordDispatched(string channel, string outcome) =>
        _dispatched.Add(1,
            new KeyValuePair<string, object?>("channel", channel),
            new KeyValuePair<string, object?>("outcome", outcome));

    public void RecordDispatchDuration(string channel, double milliseconds) =>
        _dispatchDuration.Record(milliseconds, new KeyValuePair<string, object?>("channel", channel));

    public void RecordInboundDiscarded() => _inboundDiscarded.Add(1);

    public void SetQueueDepth(long depth) => Interlocked.Exchange(ref _queueDepth, depth);

    public void Dispose() => _meter.Dispose();
}
