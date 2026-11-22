using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateTaskList;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateTaskList;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTaskLists;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Agenda;

/// <summary>
/// Promoting a task list to default must demote the previous one, so the partial unique index
/// (one default per user) always holds — the same guarantee the calendar default has.
/// </summary>
[Collection("Integration")]
public sealed class TaskListDefaultTests : IAsyncLifetime
{
    private readonly PandoraWebApplicationFactory _factory;
    private readonly Guid _userId = Guid.NewGuid();

    public TaskListDefaultTests(PandoraWebApplicationFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Promoting_a_list_to_default_demotes_the_previous_default()
    {
        var inbox = (await SendAsync(new CreateTaskListCommand(
            new CreateTaskListInput(_userId, "Inbox", IsDefault: true)))).Value;
        var later = (await SendAsync(new CreateTaskListCommand(
            new CreateTaskListInput(_userId, "Later", IsDefault: false)))).Value;

        var updated = await SendAsync(new UpdateTaskListCommand(new UpdateTaskListInput(
            _userId, later.Id, Name: null, Position: null, IsDefault: true)));
        Assert.True(updated.IsSuccess);

        var lists = (await QueryAsync(new GetTaskListsQuery(new GetTaskListsInput(_userId)))).Value;

        Assert.Equal(1, lists.Count(l => l.IsDefault));
        Assert.True(lists.Single(l => l.Id == later.Id).IsDefault);
        Assert.False(lists.Single(l => l.Id == inbox.Id).IsDefault);
    }

    // ── helpers ──

    private async Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command)
        where TResult : notnull
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(command, CancellationToken.None);
    }

    private async Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query)
        where TResult : notnull
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(query, CancellationToken.None);
    }
}
