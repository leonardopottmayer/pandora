using Pottmayer.Pandora.Modules.Assistant.Application.Commands.CancelInvocation;
using Pottmayer.Pandora.Modules.Assistant.Application.Commands.ConfirmInvocation;
using Pottmayer.Pandora.Modules.Assistant.Application.Dtos;
using Pottmayer.Pandora.Modules.Assistant.Application.Interpret;
using Pottmayer.Pandora.Modules.Assistant.Application.Subscribers;
using Pottmayer.Pandora.Modules.Assistant.Domain.Errors;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;
using Pottmayer.Pandora.Modules.Channels.Contracts;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Assistant.Tests;

public sealed class AssistantInteractionReceivedHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid InvocationId = Guid.NewGuid();

    private static (AssistantInteractionReceivedHandler Handler, FakeSender Sender, FakeIntegrationEventBus Bus) Build(
        Result<InvocationResultDto> response)
    {
        var context = new FakeDataContext();
        context.Register<IAssistantProfileRepository>(new FakeAssistantProfileRepository());
        var factory = new FakeUnitOfWorkFactory(context);
        var sender = new FakeSender { Response = response };
        var bus = new FakeIntegrationEventBus();
        var handler = new AssistantInteractionReceivedHandler(
            factory, sender, bus,
            new AssistantToolContextResolver(factory, FakeEffectiveTimeZoneResolver.With("America/Sao_Paulo")),
            new FixedTimeProvider(Now));
        return (handler, sender, bus);
    }

    private static InboundInteractionReceived Tap(string action, string owner = "assistant", string? payload = null) =>
        new(Guid.NewGuid(), Now, User, "telegram", owner, action, payload ?? InvocationId.ToString());

    private static Result<InvocationResultDto> Outcome(string status, string message) =>
        Result<InvocationResultDto>.Success(new InvocationResultDto(InvocationId, status, "create_reminder", "{}", message));

    [Fact]
    public async Task Confirm_runs_the_held_call_and_replies_with_its_outcome()
    {
        var (handler, sender, bus) = Build(Outcome("executed", "Lembrete criado."));

        await handler.HandleAsync(Tap("confirm"));

        var input = Assert.IsType<ConfirmInvocationCommand>(Assert.Single(sender.Sent)).Input;
        Assert.Equal((User, InvocationId), (input.UserId, input.InvocationId));
        var reply = Assert.IsType<SendAssistantReply>(Assert.Single(bus.Published));
        Assert.Equal(("assistant", "Lembrete criado."), (reply.Bot, reply.Text));
    }

    [Fact]
    public async Task Cancel_declines_the_held_call()
    {
        var (handler, sender, bus) = Build(Outcome("cancelled", "Cancelado."));

        await handler.HandleAsync(Tap("cancel"));

        Assert.IsType<CancelInvocationCommand>(Assert.Single(sender.Sent));
        Assert.Equal("Cancelado.", Assert.IsType<SendAssistantReply>(Assert.Single(bus.Published)).Text);
    }

    [Fact]
    public async Task An_expired_confirmation_is_explained_in_the_users_language()
    {
        var (handler, _, bus) = Build(Result<InvocationResultDto>.Failure(AssistantErrors.ConfirmationExpired));

        await handler.HandleAsync(Tap("confirm"));

        Assert.Equal("Essa confirmação expirou. Peça de novo.", Assert.IsType<SendAssistantReply>(Assert.Single(bus.Published)).Text);
    }

    [Fact]
    public async Task Ignores_buttons_it_does_not_own()
    {
        var (handler, sender, bus) = Build(Outcome("executed", "x"));

        await handler.HandleAsync(Tap("task_done", owner: "agenda"));
        await handler.HandleAsync(Tap("confirm", payload: "not-a-guid"));

        Assert.Empty(sender.Sent);
        Assert.Empty(bus.Published);
    }
}
