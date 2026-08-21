using Pottmayer.Pandora.Modules.Agenda.Application.Commands.AcknowledgeReminder;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.SnoozeReminder;
using Pottmayer.Pandora.Modules.Agenda.Application.Subscribers;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Mediator.Abstractions.Messaging;
using Xunit;

namespace Pottmayer.Pandora.Modules.Agenda.Tests;

public sealed class InboundInteractionReceivedHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid User = Guid.NewGuid();

    private sealed class RecordingSender : ISender
    {
        public List<object> Sent { get; } = [];

        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return ValueTask.FromResult(default(TResponse)!);
        }
    }

    private static InboundInteractionReceived Tap(string action, string? payload, string ownerModule = "agenda") =>
        new(Guid.NewGuid(), Now, User, "telegram", ownerModule, action, payload);

    [Fact]
    public async Task Task_done_acknowledges_the_reminder()
    {
        var sender = new RecordingSender();
        var reminderId = Guid.NewGuid();

        await new InboundInteractionReceivedHandler(sender, new FixedTimeProvider(Now))
            .HandleAsync(Tap("task_done", reminderId.ToString()));

        var command = Assert.IsType<AcknowledgeReminderCommand>(Assert.Single(sender.Sent));
        Assert.Equal(User, command.Input.UserId);
        Assert.Equal(reminderId, command.Input.ReminderId);
    }

    [Fact]
    public async Task Snooze_1h_snoozes_an_hour_out()
    {
        var sender = new RecordingSender();
        var reminderId = Guid.NewGuid();

        await new InboundInteractionReceivedHandler(sender, new FixedTimeProvider(Now))
            .HandleAsync(Tap("snooze_1h", reminderId.ToString()));

        var command = Assert.IsType<SnoozeReminderCommand>(Assert.Single(sender.Sent));
        Assert.Equal(reminderId, command.Input.ReminderId);
        Assert.Equal(Now.AddHours(1), command.Input.SnoozedUntil);
    }

    [Fact]
    public async Task Interactions_for_other_modules_are_ignored()
    {
        var sender = new RecordingSender();

        await new InboundInteractionReceivedHandler(sender, new FixedTimeProvider(Now))
            .HandleAsync(Tap("task_done", Guid.NewGuid().ToString(), ownerModule: "notes"));

        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task A_payload_that_is_not_a_reminder_id_is_ignored()
    {
        var sender = new RecordingSender();

        await new InboundInteractionReceivedHandler(sender, new FixedTimeProvider(Now))
            .HandleAsync(Tap("task_done", "not-a-guid"));

        Assert.Empty(sender.Sent);
    }
}
