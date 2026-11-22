using Pottmayer.Pandora.Modules.Channels.Application.Queries.GetDeliveryHistory;
using Pottmayer.Pandora.Modules.Channels.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Channels.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Channels.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Channels.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Channels.Tests;

public sealed class DeliveryHistoryQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    private static Notification Row(Guid userId, Channel channel, string address, DateTimeOffset at)
        => Notification.Queue(
            channel,
            NotificationAddress.Create(channel, address),
            TemplateKey.Create("agenda.reminder.due"),
            "pt-BR", "{}", new NotificationContent("Subject", "Body", false),
            Guid.NewGuid(), new FixedTimeProvider(at), userId: userId, category: "agenda.reminder");

    private static GetDeliveryHistoryQueryHandler Build(params Notification[] seed)
    {
        var ctx = new FakeDataContext()
            .Register<INotificationRepository>(new FakeNotificationRepository(seed));
        return new GetDeliveryHistoryQueryHandler(new FakeUnitOfWorkFactory(ctx));
    }

    private static GetDeliveryHistoryQuery Query(string? status = null) =>
        new(new GetDeliveryHistoryInput(User, status, null, null, null, null, 0, 50));

    [Fact]
    public async Task Returns_only_the_callers_notifications()
    {
        var handler = Build(
            Row(User, Channel.Email, "me@example.com", Now),
            Row(Other, Channel.Email, "someone@example.com", Now));

        var result = await handler.Handle(Query());

        Assert.True(result.IsSuccess);
        var only = Assert.Single(result.Value!);
        Assert.Equal("agenda.reminder", only.Category);
    }

    [Fact]
    public async Task Filters_by_status()
    {
        var sent = Row(User, Channel.Email, "me@example.com", Now);
        sent.MarkSent("email", null);
        var pending = Row(User, Channel.Telegram, "123456789", Now);

        var result = await Build(sent, pending).Handle(Query(status: "Sent"));

        var only = Assert.Single(result.Value!);
        Assert.Equal("Sent", only.Status);
    }

    [Fact]
    public async Task Orders_newest_first()
    {
        var older = Row(User, Channel.Email, "me@example.com", Now.AddHours(-2));
        var newer = Row(User, Channel.Telegram, "123456789", Now);

        var result = await Build(older, newer).Handle(Query());

        Assert.Equal(newer.Id, result.Value![0].Id);
        Assert.Equal(older.Id, result.Value![1].Id);
    }
}
