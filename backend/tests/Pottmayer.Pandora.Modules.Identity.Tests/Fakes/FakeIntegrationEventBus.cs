using Pottmayer.Tars.Messaging.Abstractions;

namespace Pottmayer.Pandora.Modules.Identity.Tests.Fakes;

public sealed class FakeIntegrationEventBus : IIntegrationEventBus
{
    public List<IIntegrationEvent> Published { get; } = [];

    public Task PublishAsync(IIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        Published.Add(@event);
        return Task.CompletedTask;
    }
}
