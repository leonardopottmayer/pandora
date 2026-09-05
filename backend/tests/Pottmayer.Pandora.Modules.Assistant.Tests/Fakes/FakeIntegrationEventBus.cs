using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;

/// <summary>Captures what was published, in order.</summary>
internal sealed class FakeIntegrationEventBus : IIntegrationEventBus
{
    public List<IIntegrationEvent> Published { get; } = [];

    public Task PublishAsync(IIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        Published.Add(@event);
        return Task.CompletedTask;
    }
}
