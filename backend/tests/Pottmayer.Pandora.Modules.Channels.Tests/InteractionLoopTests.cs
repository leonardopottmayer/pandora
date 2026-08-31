using Microsoft.Extensions.Logging.Abstractions;
using Pottmayer.Pandora.Modules.Channels.Application.Enqueue;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.Rendering;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Channels.Infrastructure.Ingress;
using Pottmayer.Pandora.Modules.Channels.Tests.Fakes;
using Pottmayer.Tars.Communication.Telegram.Abstractions.Models;
using Xunit;

namespace Pottmayer.Pandora.Modules.Channels.Tests;

public sealed class InteractionLoopTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private const string ChatId = "123456789";

    private readonly FixedTimeProvider _time = new(Now);

    // ── Outbound: buttons become interaction rows + a rendered keyboard ──

    [Fact]
    public async Task Telegram_buttons_register_interactions_and_render_a_keyboard()
    {
        var notifications = new FakeNotificationRepository();
        var interactions = new FakeInteractionRepository();
        var renderer = new FakeTemplateRenderer { Content = new NotificationContent(string.Empty, "Pagar a conta", false) };
        var ctx = new FakeDataContext()
            .Register<INotificationRepository>(notifications)
            .Register<IInteractionRepository>(interactions);
        var enqueuer = new NotificationEnqueuer(new FakeUnitOfWorkFactory(ctx), renderer, _time);
        var userId = Guid.NewGuid();

        await enqueuer.EnqueueAsync(
            Channel.Telegram, ChatId, TemplateKey.Create("agenda.reminder.due"), "pt-BR",
            new Dictionary<string, string>(), Guid.NewGuid(),
            userId: userId,
            buttons: [new NotificationButton("agenda", "task_done", "✓ Feito"),
                      new NotificationButton("agenda", "snooze_1h", "⏰ Adiar 1h", "{\"reminderId\":\"r1\"}")]);

        var notification = Assert.Single(notifications.Added);
        Assert.Equal(2, interactions.Added.Count);
        Assert.All(interactions.Added, i => Assert.Equal(userId, i.UserId));
        Assert.All(interactions.Added, i => Assert.Equal(notification.Id, i.NotificationId));

        var payload = TelegramRenderedPayload.Deserialize(notification.RenderedPayload);
        Assert.NotNull(payload);
        Assert.Equal("Pagar a conta", payload!.Text);
        Assert.Equal(2, payload.Buttons.Count);
        // The callback data is the interaction id, not the action — that is the whole indirection.
        Assert.Equal(interactions.Added[0].Id.ToString(), payload.Buttons[0].InteractionId);
        Assert.Equal("✓ Feito", payload.Buttons[0].Label);
    }

    [Fact]
    public async Task Email_ignores_buttons()
    {
        var notifications = new FakeNotificationRepository();
        var interactions = new FakeInteractionRepository();
        var ctx = new FakeDataContext()
            .Register<INotificationRepository>(notifications)
            .Register<IInteractionRepository>(interactions);
        var enqueuer = new NotificationEnqueuer(new FakeUnitOfWorkFactory(ctx), new FakeTemplateRenderer(), _time);

        await enqueuer.EnqueueAsync(
            Channel.Email, "alice@example.com", TemplateKey.Create("agenda.reminder.due"), "en",
            new Dictionary<string, string>(), Guid.NewGuid(),
            userId: Guid.NewGuid(),
            buttons: [new NotificationButton("agenda", "task_done", "Done")]);

        Assert.Empty(interactions.Added);
        Assert.Null(Assert.Single(notifications.Added).RenderedPayload);
    }

    // ── Inbound: a tapped button routes back and is single use ──

    private (TelegramInboundTriage Triage, FakeIntegrationEventBus Bus, FakeInteractionRepository Interactions, FakeTelegramClient Client)
        BuildTriage(Guid userId, params Interaction[] interactions)
    {
        var linked = UserChannel.LinkVerified(
            userId, Channel.Telegram, NotificationAddress.Create(Channel.Telegram, ChatId), "pt-BR", "{}", _time);
        var interactionRepo = new FakeInteractionRepository(interactions);
        var bus = new FakeIntegrationEventBus();
        var client = new FakeTelegramClient();
        var ctx = new FakeDataContext()
            .Register<IInboundUpdateRepository>(new FakeInboundUpdateRepository())
            .Register<IUserChannelRepository>(new FakeUserChannelRepository(linked))
            .Register<IInteractionRepository>(interactionRepo);
        var triage = new TelegramInboundTriage(
            new FakeUnitOfWorkFactory(ctx), bus, new FakeSender(), client, new FakeChannelsMetrics(), _time,
            NullLogger<TelegramInboundTriage>.Instance);
        return (triage, bus, interactionRepo, client);
    }

    private static TelegramUpdate Callback(long updateId, string data) =>
        new(updateId, CallbackQuery: new TelegramCallbackQuery(
            "cb-1", new TelegramSender(long.Parse(ChatId)), Data: data,
            Chat: new TelegramChat(long.Parse(ChatId), "private"), MessageId: 1));

    [Fact]
    public async Task A_valid_tap_routes_the_action_back_and_burns_the_button()
    {
        var userId = Guid.NewGuid();
        var interaction = Interaction.Register(
            userId, "agenda", "task_done", "{\"reminderId\":\"r1\"}", Guid.NewGuid(), Now.AddHours(1), _time);
        var (triage, bus, interactions, client) = BuildTriage(userId, interaction);

        await triage.HandleAsync(Callback(20, interaction.Id.ToString()), CancellationToken.None);

        var evt = Assert.IsType<InboundInteractionReceived>(Assert.Single(bus.Published));
        Assert.Equal(userId, evt.UserId);
        Assert.Equal("agenda", evt.OwnerModule);
        Assert.Equal("task_done", evt.Action);
        Assert.Equal("{\"reminderId\":\"r1\"}", evt.Payload);

        Assert.Single(interactions.Updated); // consumed
        Assert.Equal("cb-1", Assert.Single(client.AnsweredCallbacks));
    }

    [Fact]
    public async Task An_expired_button_is_answered_but_not_routed()
    {
        var userId = Guid.NewGuid();
        var expired = Interaction.Register(
            userId, "agenda", "task_done", null, Guid.NewGuid(), Now.AddHours(-1), _time);
        var (triage, bus, interactions, client) = BuildTriage(userId, expired);

        await triage.HandleAsync(Callback(21, expired.Id.ToString()), CancellationToken.None);

        Assert.Empty(bus.Published);
        Assert.Empty(interactions.Updated);
        Assert.Single(client.AnsweredCallbacks);
    }

    [Fact]
    public async Task A_tap_on_someone_elses_button_is_refused()
    {
        var owner = Guid.NewGuid();
        var otherUsersButton = Interaction.Register(
            Guid.NewGuid(), "agenda", "task_done", null, Guid.NewGuid(), Now.AddHours(1), _time);
        // The chat belongs to `owner`, but the interaction belongs to someone else.
        var (triage, bus, interactions, _) = BuildTriage(owner, otherUsersButton);

        await triage.HandleAsync(Callback(22, otherUsersButton.Id.ToString()), CancellationToken.None);

        Assert.Empty(bus.Published);
        Assert.Empty(interactions.Updated);
    }
}
