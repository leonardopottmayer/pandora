using Microsoft.Extensions.Logging.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Application.Commands.Interpret;
using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Pandora.Modules.Assistant.Application.Subscribers;
using Pottmayer.Pandora.Modules.Assistant.Domain.Errors;
using Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Assistant.Tests;

public sealed class InboundMessageReceivedHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly FixedTimeProvider _time = new(Now);

    private (InboundMessageReceivedHandler Handler, FakeSender Sender, FakeIntegrationEventBus Bus) Build(
        Result<InterpretResultDto>? response = null)
    {
        var sender = new FakeSender { Response = response ?? Result<InterpretResultDto>.Success(Dto("feito ✓")) };
        var bus = new FakeIntegrationEventBus();
        var handler = new InboundMessageReceivedHandler(
            new FakeUnitOfWorkFactory(new FakeDataContext()), sender, bus, _time,
            NullLogger<InboundMessageReceivedHandler>.Instance);
        return (handler, sender, bus);
    }

    private static InterpretResultDto Dto(string message) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "executed", "create_reminder", "{}", message);

    private static InboundMessageReceived Inbound(string bot, string? text) =>
        new(Guid.NewGuid(), Now, Guid.NewGuid(), "telegram", bot, text, MediaRef: null, MediaMimeType: null);

    [Fact]
    public async Task Interprets_an_assistant_bot_message_and_publishes_the_reply()
    {
        var (handler, sender, bus) = Build(Result<InterpretResultDto>.Success(Dto("lembrete criado ✓")));
        var inbound = Inbound("assistant", "me lembra de pagar a conta amanhã");

        await handler.HandleAsync(inbound);

        Assert.IsType<InterpretCommand>(Assert.Single(sender.Sent));
        var reply = Assert.IsType<SendAssistantReply>(Assert.Single(bus.Published));
        Assert.Equal(inbound.UserId, reply.UserId);
        Assert.Equal("assistant", reply.Bot);
        Assert.Equal("lembrete criado ✓", reply.Text);
    }

    [Fact]
    public async Task Ignores_a_message_from_another_bot()
    {
        var (handler, sender, bus) = Build();

        await handler.HandleAsync(Inbound("notifications", "oi"));

        Assert.Empty(sender.Sent);
        Assert.Empty(bus.Published);
    }

    [Fact]
    public async Task Ignores_a_message_with_no_text()
    {
        var (handler, sender, bus) = Build();

        await handler.HandleAsync(Inbound("assistant", "   "));

        Assert.Empty(sender.Sent);
        Assert.Empty(bus.Published);
    }

    [Fact]
    public async Task Does_not_reply_when_interpret_is_rejected()
    {
        var (handler, sender, bus) = Build(Result<InterpretResultDto>.Failure(AssistantErrors.NotEnabled));

        await handler.HandleAsync(Inbound("assistant", "faz algo"));

        Assert.Single(sender.Sent);
        Assert.Empty(bus.Published);
    }

    [Fact]
    public async Task Does_not_reply_when_the_interpretation_has_no_message()
    {
        var (handler, sender, bus) = Build(Result<InterpretResultDto>.Success(Dto("   ")));

        await handler.HandleAsync(Inbound("assistant", "faz algo"));

        Assert.Single(sender.Sent);
        Assert.Empty(bus.Published);
    }
}
