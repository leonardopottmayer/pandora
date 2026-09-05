using Pottmayer.Pandora.Modules.Assistant.Application.Commands.CancelInvocation;
using Pottmayer.Pandora.Modules.Assistant.Application.Commands.ConfirmInvocation;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
using Pottmayer.Pandora.Modules.Assistant.Tests.Fakes;
using Xunit;

namespace Pottmayer.Pandora.Modules.Assistant.Tests;

public sealed class ConfirmInvocationCommandHandlerTests
{
    private static readonly Guid User = Guid.NewGuid();

    private static CommandInvocation Pending(DateTimeOffset now, TimeSpan? ttl = null) =>
        CommandInvocation.Create(
            User, Guid.CreateVersion7(), "lembrete de pagar o aluguel",
            "create_reminder", """{ "title": "Aluguel", "remindAt": "2026-09-05T10:00:00-03:00" }""",
            InvocationStatus.PendingConfirmation, "Confirmar create_reminder?", null,
            "gemini", "gemini-3.6-flash", 12, 3, 4, now + (ttl ?? TimeSpan.FromMinutes(10)),
            new FixedTimeProvider(now));

    private static (FakeUnitOfWorkFactory Factory, FakeCommandInvocationRepository Invocations) Context(
        params CommandInvocation[] seed)
    {
        var invocations = new FakeCommandInvocationRepository();
        foreach (var i in seed)
            invocations.AddAsync(i);
        invocations.Added.Clear(); // seeding is not part of the assertion surface
        var context = new FakeDataContext();
        context.Register<ICommandInvocationRepository>(invocations);
        return (new FakeUnitOfWorkFactory(context), invocations);
    }

    [Fact]
    public async Task Confirming_runs_the_stored_tool_call_and_marks_it_executed()
    {
        var now = DateTimeOffset.UtcNow;
        var pending = Pending(now);
        var (factory, invocations) = Context(pending);
        var command = FakeAssistantTool.Succeeds("create_reminder", "Lembrete criado.");
        var handler = new ConfirmInvocationCommandHandler(factory, [command], new FixedTimeProvider(now));

        var result = await handler.Handle(
            new ConfirmInvocationCommand(new ConfirmInvocationInput(User, pending.Id)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InvocationStatus.Executed.Value, result.Value!.Status);
        Assert.Equal("Lembrete criado.", result.Value.Message);
        Assert.Equal(1, command.Calls);
        Assert.Equal("Aluguel", command.LastArguments!.Value.GetProperty("title").GetString());
        Assert.Equal(InvocationStatus.Executed, pending.Status);
        Assert.Contains(pending, invocations.Updated);
    }

    [Fact]
    public async Task Confirming_an_expired_pending_is_refused_and_marked_expired()
    {
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-20);
        var pending = Pending(createdAt); // expires 10 min after createdAt → already past
        var (factory, _) = Context(pending);
        var command = FakeAssistantTool.Succeeds("create_reminder");
        var handler = new ConfirmInvocationCommandHandler(factory, [command], TimeProvider.System);

        var result = await handler.Handle(
            new ConfirmInvocationCommand(new ConfirmInvocationInput(User, pending.Id)), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, command.Calls);
        Assert.Equal(InvocationStatus.Expired, pending.Status);
    }

    [Fact]
    public async Task Confirming_someone_elses_invocation_is_not_found()
    {
        var now = DateTimeOffset.UtcNow;
        var pending = Pending(now);
        var (factory, _) = Context(pending);
        var handler = new ConfirmInvocationCommandHandler(
            factory, [FakeAssistantTool.Succeeds("create_reminder")], new FixedTimeProvider(now));

        var result = await handler.Handle(
            new ConfirmInvocationCommand(new ConfirmInvocationInput(Guid.NewGuid(), pending.Id)), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Cancelling_a_pending_marks_it_cancelled()
    {
        var now = DateTimeOffset.UtcNow;
        var pending = Pending(now);
        var (factory, invocations) = Context(pending);
        var handler = new CancelInvocationCommandHandler(factory);

        var result = await handler.Handle(
            new CancelInvocationCommand(new CancelInvocationInput(User, pending.Id)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InvocationStatus.Cancelled.Value, result.Value!.Status);
        Assert.Equal(InvocationStatus.Cancelled, pending.Status);
        Assert.Contains(pending, invocations.Updated);
    }

    [Fact]
    public async Task Cancelling_a_non_pending_invocation_is_refused()
    {
        var now = DateTimeOffset.UtcNow;
        var executed = CommandInvocation.Create(
            User, Guid.CreateVersion7(), "x", "create_reminder", "{}",
            InvocationStatus.Executed, "done", null, "gemini", "m", 1, 0, 0, null, new FixedTimeProvider(now));
        var (factory, _) = Context(executed);
        var handler = new CancelInvocationCommandHandler(factory);

        var result = await handler.Handle(
            new CancelInvocationCommand(new CancelInvocationInput(User, executed.Id)), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
