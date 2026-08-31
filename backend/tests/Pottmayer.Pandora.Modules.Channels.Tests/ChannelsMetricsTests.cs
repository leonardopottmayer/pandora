using System.Diagnostics.Metrics;
using Pottmayer.Pandora.Modules.Channels.Application.Commands.DispatchPending;
using Pottmayer.Pandora.Modules.Channels.Infrastructure.Observability;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Channels.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Channels.Tests;

/// <summary>
/// Guards the meter's public surface: instrument names and the meter name the host subscribes to by
/// string. A rename here silently drops the module's telemetry, so it is worth locking down.
/// </summary>
public sealed class ChannelsMetricsTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Dispatching_emits_the_dispatched_counter_and_queue_depth_gauge()
    {
        var metrics = new ChannelsMetrics();
        var longMeasurements = new List<(string Instrument, long Value, string? Outcome)>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == ChannelsMetrics.MeterName)
                    l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            string? outcome = null;
            foreach (var tag in tags)
                if (tag.Key == "outcome")
                    outcome = tag.Value as string;
            lock (longMeasurements)
                longMeasurements.Add((instrument.Name, value, outcome));
        });
        listener.Start();

        var time = new FixedTimeProvider(Now);
        var transport = new FakeChannelTransport(Channel.Email) { Provider = "ses", ProviderMessageId = "m1" };
        var notification = Notification.Queue(
            Channel.Email, NotificationAddress.Create(Channel.Email, "alice@example.com"),
            TemplateKey.Create("account-activation"), "en", "{}",
            new NotificationContent("Subject", "Body", false), Guid.NewGuid(), time);

        var repo = new FakeNotificationRepository(notification);
        var ctx = new FakeDataContext()
            .Register<INotificationRepository>(repo)
            .Register<IUserChannelRepository>(new FakeUserChannelRepository());
        var handler = new DispatchPendingNotificationsCommandHandler(
            new FakeUnitOfWorkFactory(ctx), [transport], new FakeIntegrationEventBus(), metrics, time);

        await handler.Handle(new DispatchPendingNotificationsCommand(new DispatchPendingNotificationsInput(20)), CancellationToken.None);

        // Force the observable gauge to be read.
        listener.RecordObservableInstruments();

        Assert.Contains(longMeasurements, m => m.Instrument == "channels.notifications.dispatched" && m.Outcome == "sent");
        // Nothing left in flight after the only notification was sent.
        Assert.Contains(longMeasurements, m => m.Instrument == "channels.notifications.queue.depth" && m.Value == 0);
    }
}
