using Pottmayer.Pandora.Modules.Identity.Contracts.IntegrationEvents;
using Pottmayer.Pandora.Modules.Notifications.Application.Enqueue;
using Pottmayer.Pandora.Modules.Notifications.Application.Subscribers;
using Pottmayer.Pandora.Modules.Notifications.Contracts;
using Pottmayer.Pandora.Modules.Notifications.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Notifications.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Notifications.Tests;

/// <summary>
/// The subscribers map an integration event to an enqueue call. They are exercised against a real
/// <see cref="NotificationEnqueuer"/> (with fakes underneath) so the mapping is verified end-to-end.
/// </summary>
public sealed class SubscriberTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static (NotificationEnqueuer Enqueuer, FakeNotificationRepository Repo) Build()
    {
        var repo = new FakeNotificationRepository();
        var ctx = new FakeDataContext().Register<INotificationRepository>(repo);
        var enqueuer = new NotificationEnqueuer(new FakeUnitOfWorkFactory(ctx), new FakeTemplateRenderer(), new FixedTimeProvider(Now));
        return (enqueuer, repo);
    }

    [Fact]
    public async Task AccountActivation_maps_to_the_activation_template_email()
    {
        var (enqueuer, repo) = Build();
        var handler = new AccountActivationRequestedHandler(enqueuer);
        var eventId = Guid.NewGuid();
        var @event = new AccountActivationRequested(
            eventId, Now, UserId: Guid.NewGuid(), Email: "Carol@Example.com", Token: "tok-1", Locale: "pt-BR");

        await handler.HandleAsync(@event);

        var n = Assert.Single(repo.Added);
        Assert.Equal("email", n.Channel.Value);
        Assert.Equal("carol@example.com", n.Recipient.Value);
        Assert.Equal("account-activation", n.TemplateKey.Value);
        Assert.Equal("pt-BR", n.Locale);
        Assert.Equal(eventId, n.CorrelationId);
    }

    [Fact]
    public async Task AccountActivation_normalizes_unsupported_locale_to_default()
    {
        var (enqueuer, repo) = Build();
        var handler = new AccountActivationRequestedHandler(enqueuer);
        var @event = new AccountActivationRequested(
            Guid.NewGuid(), Now, Guid.NewGuid(), "dave@example.com", "tok", Locale: "fr");

        await handler.HandleAsync(@event);

        Assert.Equal("en", Assert.Single(repo.Added).Locale);
    }

    [Fact]
    public async Task PasswordResetRequested_maps_to_the_password_reset_template_email()
    {
        var (enqueuer, repo) = Build();
        var handler = new PasswordResetRequestedHandler(enqueuer);
        var eventId = Guid.NewGuid();
        var @event = new PasswordResetRequested(
            eventId, Now, UserId: Guid.NewGuid(), Email: "Frank@Example.com", Token: "tok-2", Locale: "pt-BR");

        await handler.HandleAsync(@event);

        var n = Assert.Single(repo.Added);
        Assert.Equal("email", n.Channel.Value);
        Assert.Equal("frank@example.com", n.Recipient.Value);
        Assert.Equal("password-reset", n.TemplateKey.Value);
        Assert.Equal("pt-BR", n.Locale);
        Assert.Equal(eventId, n.CorrelationId);
    }

    [Fact]
    public async Task PasswordChanged_maps_to_the_password_changed_template_email()
    {
        var (enqueuer, repo) = Build();
        var handler = new PasswordChangedHandler(enqueuer);
        var eventId = Guid.NewGuid();
        var @event = new PasswordChanged(
            eventId, Now, UserId: Guid.NewGuid(), Email: "grace@example.com", Locale: "en");

        await handler.HandleAsync(@event);

        var n = Assert.Single(repo.Added);
        Assert.Equal("password-changed", n.TemplateKey.Value);
        Assert.Equal(eventId, n.CorrelationId);
    }

    [Fact]
    public async Task SendNotificationRequested_uses_the_caller_supplied_channel_and_template()
    {
        var (enqueuer, repo) = Build();
        var handler = new SendNotificationRequestedHandler(enqueuer);
        var eventId = Guid.NewGuid();
        var @event = new SendNotificationRequested(
            eventId, Now, Channel: "email", Recipient: "erin@example.com", TemplateKey: "Custom-Welcome",
            Locale: "en", Payload: new Dictionary<string, string> { ["name"] = "Erin" });

        await handler.HandleAsync(@event);

        var n = Assert.Single(repo.Added);
        Assert.Equal("email", n.Channel.Value);
        Assert.Equal("erin@example.com", n.Recipient.Value);
        Assert.Equal("custom-welcome", n.TemplateKey.Value);
        Assert.Equal(eventId, n.CorrelationId);
    }
}
