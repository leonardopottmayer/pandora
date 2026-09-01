using Pottmayer.Pandora.Modules.Channels.Application.Subscribers;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Pandora.Modules.Channels.Tests.Fakes;
using Pottmayer.Pandora.Modules.Integrations.Contracts;
using Xunit;

namespace Pottmayer.Pandora.Modules.Channels.Tests;

/// <summary>
/// The revoked handler turns Integrations' fact into a per-user "reconnect" notification. It has no
/// address to send to, so it republishes <see cref="NotifyUserRequested"/> and lets the fan-out path
/// resolve the user's channels.
/// </summary>
public sealed class ExternalAccountRevokedHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static (ExternalAccountRevokedHandler Handler, FakeIntegrationEventBus Bus) Build()
    {
        var bus = new FakeIntegrationEventBus();
        var factory = new FakeUnitOfWorkFactory(new FakeDataContext());
        return (new ExternalAccountRevokedHandler(factory, bus), bus);
    }

    [Fact]
    public async Task Publishes_a_reconnect_notification_for_the_revoked_account()
    {
        var (handler, bus) = Build();
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        await handler.HandleAsync(new ExternalAccountRevoked(
            eventId, Now, userId, ExternalAccountId: Guid.NewGuid(), Provider: "google"));

        var notify = Assert.IsType<NotifyUserRequested>(Assert.Single(bus.Published));
        Assert.Equal(userId, notify.UserId);
        Assert.Equal("integrations.account", notify.Category);
        Assert.Equal("integrations.account-revoked", notify.TemplateKey);
        Assert.Null(notify.Channels); // fan out to whatever the user has
        Assert.Equal(eventId, notify.CorrelationId); // de-dups against a re-delivery
        Assert.Equal("Google", notify.Payload["provider"]); // title-cased for display
    }
}
