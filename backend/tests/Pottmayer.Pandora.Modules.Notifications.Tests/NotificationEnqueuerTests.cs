using System.Text.Json;
using Pottmayer.Pandora.Modules.Notifications.Application.Enqueue;
using Pottmayer.Pandora.Modules.Notifications.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Notifications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notifications.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Notifications.Tests.Fakes;
using Pottmayer.Pandora.Shared.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notifications.Tests;

public sealed class NotificationEnqueuerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static (NotificationEnqueuer Enqueuer, FakeNotificationRepository Repo, FakeTemplateRenderer Renderer) Build(
        params Notification[] existing)
    {
        var repo = new FakeNotificationRepository(existing);
        var renderer = new FakeTemplateRenderer();
        var ctx = new FakeDataContext().Register<INotificationRepository>(repo);
        var enqueuer = new NotificationEnqueuer(new FakeUnitOfWorkFactory(ctx), renderer, new FixedTimeProvider(Now));
        return (enqueuer, repo, renderer);
    }

    [Fact]
    public async Task Renders_and_persists_a_pending_notification()
    {
        var (enqueuer, repo, renderer) = Build();
        renderer.Content = new NotificationContent("Hello", "World", IsHtml: true);
        var correlationId = Guid.NewGuid();
        var payload = new Dictionary<string, string> { ["token"] = "abc" };

        await enqueuer.EnqueueAsync(
            Channel.Email, "Alice@Example.com", TemplateKey.Create("account-activation"), "pt-BR", payload, correlationId);

        var n = Assert.Single(repo.Added);
        Assert.Equal(NotificationStatus.Pending, n.Status);
        Assert.Equal("alice@example.com", n.Recipient.Value); // Email normalizes
        Assert.Equal("account-activation", n.TemplateKey.Value);
        Assert.Equal("pt-BR", n.Locale);
        Assert.Equal("Hello", n.Subject);
        Assert.Equal("World", n.Body);
        Assert.True(n.IsHtml);
        Assert.Equal(correlationId, n.CorrelationId);
        Assert.Equal(payload, JsonSerializer.Deserialize<Dictionary<string, string>>(n.Payload));

        var call = Assert.Single(renderer.Calls);
        Assert.Equal("account-activation", call.TemplateKey.Value);
        Assert.Equal("pt-BR", call.Locale);
    }

    [Fact]
    public async Task Is_idempotent_on_correlation_id()
    {
        var correlationId = Guid.NewGuid();
        var existing = Notification.Queue(
            Channel.Email, Email.Create("bob@example.com"), TemplateKey.Create("account-activation"),
            "en", "{}", new NotificationContent("s", "b", false), correlationId, new FixedTimeProvider(Now));
        var (enqueuer, repo, _) = Build(existing);

        await enqueuer.EnqueueAsync(
            Channel.Email, "bob@example.com", TemplateKey.Create("account-activation"), "en",
            new Dictionary<string, string>(), correlationId);

        Assert.Empty(repo.Added); // dedup: nothing new persisted
    }

    [Fact]
    public async Task Invalid_recipient_throws_before_persisting()
    {
        var (enqueuer, repo, _) = Build();

        await Assert.ThrowsAsync<ArgumentException>(() => enqueuer.EnqueueAsync(
            Channel.Email, "not-an-email", TemplateKey.Create("account-activation"), "en",
            new Dictionary<string, string>(), Guid.NewGuid()));

        Assert.Empty(repo.Added);
    }
}
