using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Assistant.Abstractions;
using Pottmayer.Pandora.Modules.Assistant.Application.Queries.GetInvocations;
using Pottmayer.Pandora.Modules.Assistant.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Assistant.Domain.Ports.Repositories;
using Pottmayer.Pandora.Modules.Assistant.Domain.ValueObjects;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Data.Abstractions.UnitOfWork;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Assistant;

/// <summary>A held call nobody answered reads as expired in the history once its window closes.</summary>
[Collection("Integration")]
public sealed class InvocationHistoryTests(PandoraWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly Guid _userId = Guid.NewGuid();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_held_call_past_its_window_reads_as_expired_and_one_inside_it_stays_pending()
    {
        var now = DateTimeOffset.UtcNow;
        await HoldAsync("delete the old list", expiresAt: now.AddMinutes(-1));
        await HoldAsync("delete the dentist", expiresAt: now.AddMinutes(9));

        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new GetInvocationsQuery(new GetInvocationsInput(_userId, 10)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(InvocationStatus.Expired.Value, Assert.Single(result.Value!, i => i.Utterance == "delete the old list").Status);
        Assert.Equal(InvocationStatus.PendingConfirmation.Value, Assert.Single(result.Value!, i => i.Utterance == "delete the dentist").Status);
    }

    private async Task HoldAsync(string utterance, DateTimeOffset expiresAt)
    {
        using var scope = factory.Services.CreateScope();
        var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>().ExecuteAsync(AssistantModule.DatabaseKey, async (context, ct) =>
        {
            var conversation = Conversation.Start(_userId, time);
            await context.AcquireRepository<IConversationRepository>().AddAsync(conversation, ct);
            await context.AcquireRepository<ICommandInvocationRepository>().AddAsync(CommandInvocation.Create(
                _userId, conversation.Id, utterance, "delete_task", "{}", InvocationStatus.PendingConfirmation,
                "Delete?", null, "gemini", "test", 1, 0, 0, expiresAt, time), ct);
            return true;
        }, cancellationToken: CancellationToken.None);
    }
}
