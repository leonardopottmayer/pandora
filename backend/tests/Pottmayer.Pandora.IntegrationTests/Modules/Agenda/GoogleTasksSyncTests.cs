using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pottmayer.Pandora.IntegrationTests.Support;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.BindGoogleTaskLists;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CompleteTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.CreateTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.DeleteTaskList;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.SyncTaskListBinding;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.UnlinkTaskList;
using Pottmayer.Pandora.Modules.Agenda.Application.Commands.UpdateTask;
using Pottmayer.Pandora.Modules.Agenda.Application.Dtos;
using Pottmayer.Pandora.Modules.Agenda.Application.Errors;
using Pottmayer.Pandora.Modules.Agenda.Application.Google;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetSyncConflicts;
using Pottmayer.Pandora.Modules.Agenda.Application.Queries.GetTasks;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Pottmayer.Tars.Core.Cqrs.Commands;
using Pottmayer.Tars.Core.Cqrs.Queries;
using Pottmayer.Tars.Core.Mediator.Abstractions;
using Pottmayer.Tars.Core.Primitives.Outcomes;
using Xunit;

namespace Pottmayer.Pandora.IntegrationTests.Modules.Agenda;

/// <summary>
/// Google Tasks ⇄ Pandora against a real database, with an in-memory Google that answers
/// <c>updatedMin</c> the way the API does: what changed since, deletions included.
/// </summary>
[Collection("Integration")]
public sealed class GoogleTasksSyncTests : IAsyncLifetime
{
    private readonly PandoraWebApplicationFactory _factory;
    private readonly WebApplicationFactory<Program> _host;
    private readonly FakeGoogleTasks _google = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _accountId = Guid.NewGuid();

    public GoogleTasksSyncTests(PandoraWebApplicationFactory factory)
    {
        _factory = factory;
        _host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGoogleTasksClient>();
            services.AddSingleton<IGoogleTasksClient>(_google);
        }));
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Google_tasks_come_in_and_pandora_edits_go_back_without_losing_what_google_lacks()
    {
        _google.Put(new GoogleTask("t1", null, "Comprar pão", "integral", "needsAction", "2026-11-20T00:00:00.000Z", null, null, null, null));
        _google.Put(new GoogleTask("t2", null, "Pagar luz", null, "completed", null, "2026-11-01T10:00:00.000Z", null, null, null));
        _google.Put(new GoogleTask("t3", null, "Ver preço", null, "needsAction", null, null, "t1", null, null));

        var binding = Assert.Single((await SendAsync(new BindGoogleTaskListsCommand(
            new BindGoogleTaskListsInput(_userId, _accountId, ["L1"])))).Value!);
        Assert.Equal("Compras", binding.ListName);
        Assert.Equal("ok", (await SendAsync(new SyncTaskListBindingNowCommand(new SyncTaskListBindingNowInput(_userId, binding.Id)))).Value!.Status);

        var tasks = await TasksAsync(binding.ListId);
        var bread = Assert.Single(tasks, t => t.Title == "Comprar pão");
        Assert.Equal("integral", bread.Notes);
        Assert.False(bread.DueHasTime);
        Assert.Equal(new DateOnly(2026, 11, 20), DateOnly.FromDateTime(bread.DueAt!.Value.UtcDateTime));
        Assert.Equal("Done", Assert.Single(tasks, t => t.Title == "Pagar luz").Status);
        Assert.Equal(bread.Id, Assert.Single(tasks, t => t.Title == "Ver preço").ParentTaskId);

        // Changes in Google: rename, delete.
        _google.Put(_google.Find("t1")! with { Title = "Comprar pão (padaria)" });
        _google.Delete("t2");
        await SyncAsync(binding.Id);
        tasks = await TasksAsync(binding.ListId);
        Assert.Contains(tasks, t => t.Title == "Comprar pão (padaria)");
        Assert.DoesNotContain(tasks, t => t.Title == "Pagar luz");

        // A task born in Pandora, with a time of day Google cannot hold.
        var due = new DateTimeOffset(2026, 11, 25, 18, 0, 0, TimeSpan.Zero);
        var created = (await SendAsync(new CreateTaskCommand(new CreateTaskInput(
            _userId, binding.ListId, null, "Ligar pro banco", null, due, true, TaskPriority.High, "UTC", null)))).Value!;
        await SyncAsync(binding.Id);
        var inGoogle = Assert.Single(_google.All, t => t.Title == "Ligar pro banco");
        Assert.Equal("2026-11-25T00:00:00.000Z", inGoogle.Due);
        var back = Assert.Single(await TasksAsync(binding.ListId), t => t.Id == created.Id);
        Assert.True(back.DueHasTime);
        Assert.Equal(due, back.DueAt);
        Assert.Equal("High", back.Priority);

        // Google moves the date: the new date is kept, Pandora's time of day goes with the old one.
        _google.Put(inGoogle with { Due = "2026-11-26T00:00:00.000Z" });
        await SyncAsync(binding.Id);
        back = Assert.Single(await TasksAsync(binding.ListId), t => t.Id == created.Id);
        Assert.False(back.DueHasTime);
        Assert.Equal("High", back.Priority);

        // Completing and deleting in Pandora reach Google.
        await SendAsync(new CompleteTaskCommand(new CompleteTaskInput(_userId, bread.Id)));
        await SendAsync(new DeleteTaskCommand(new DeleteTaskInput(_userId, created.Id)));
        await SyncAsync(binding.Id);
        Assert.Equal("completed", _google.Find("t1")!.Status);
        Assert.True(_google.Find(inGoogle.Id)!.IsDeleted);

        // Nothing moves on a quiet cycle, and a bound list cannot be deleted.
        var before = await TasksAsync(binding.ListId);
        await SyncAsync(binding.Id);
        Assert.Equal(before.Count, (await TasksAsync(binding.ListId)).Count);
        var delete = await SendAsync(new DeleteTaskListCommand(new DeleteTaskListInput(_userId, binding.ListId)));
        Assert.Equal(TaskErrors.ListBound.Code, Assert.Single(delete.Errors).Code);

        // Unlinking keeps the list and its tasks.
        Assert.True((await SendAsync(new UnlinkTaskListCommand(new UnlinkTaskListInput(_userId, binding.Id)))).Value);
        Assert.Equal(before.Count, (await TasksAsync(binding.ListId)).Count);
    }

    [Fact]
    public async Task When_both_sides_edit_a_task_the_newer_edit_wins_and_the_other_is_logged()
    {
        _google.Put(new GoogleTask("t1", null, "Original", null, "needsAction", null, null, null, null, null));
        _google.Put(new GoogleTask("t2", null, "Outra", null, "needsAction", null, null, null, null, null));
        var binding = Assert.Single((await SendAsync(new BindGoogleTaskListsCommand(
            new BindGoogleTaskListsInput(_userId, _accountId, ["L1"])))).Value!);
        await SyncAsync(binding.Id);
        var tasks = await TasksAsync(binding.ListId);
        var first = Assert.Single(tasks, t => t.Title == "Original");
        var second = Assert.Single(tasks, t => t.Title == "Outra");

        // Google's edit is newer than Pandora's: Google wins.
        await RenameAsync(first.Id, "Pandora, antes");
        _google.Put(_google.Find("t1")! with { Title = "Google, depois", Updated = DateTimeOffset.UtcNow.AddMinutes(5) });
        // Pandora's edit is newer than Google's: Pandora wins.
        _google.Put(_google.Find("t2")! with { Title = "Google, antes", Updated = DateTimeOffset.UtcNow.AddMinutes(-5) });
        await RenameAsync(second.Id, "Pandora, depois");

        await SyncAsync(binding.Id);

        tasks = await TasksAsync(binding.ListId);
        Assert.Contains(tasks, t => t.Id == first.Id && t.Title == "Google, depois");
        Assert.Contains(tasks, t => t.Id == second.Id && t.Title == "Pandora, depois");
        Assert.Equal("Pandora, depois", _google.Find("t2")!.Title);

        var conflicts = (await QueryAsync(new GetSyncConflictsQuery(new GetSyncConflictsInput(_userId)))).Value!;
        Assert.Equal(2, conflicts.Count);
        Assert.All(conflicts, c => Assert.Equal("task", c.SubjectType));
        Assert.Contains(conflicts, c => c.SubjectId == first.Id && c.LosingSide == "pandora" && c.LosingVersion.Contains("Pandora, antes"));
        Assert.Contains(conflicts, c => c.SubjectId == second.Id && c.LosingSide == "google" && c.LosingVersion.Contains("Google, antes"));
    }

    private Task RenameAsync(Guid taskId, string title) =>
        SendAsync(new UpdateTaskCommand(new UpdateTaskInput(_userId, taskId, title, null, null, false, TaskPriority.None)));

    private Task<Result<bool>> SyncAsync(Guid bindingId) =>
        SendAsync(new SyncTaskListBindingCommand(new SyncTaskListBindingInput(bindingId)));

    private async Task<IReadOnlyList<TaskDto>> TasksAsync(Guid listId) =>
        (await QueryAsync(new GetTasksQuery(new GetTasksInput(_userId, listId, null, null)))).Value!;

    private async Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command) where TResult : notnull
    {
        using var scope = _host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, CancellationToken.None);
    }

    private async Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query) where TResult : notnull
    {
        using var scope = _host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(query, CancellationToken.None);
    }

    /// <summary>One Google task list ("L1", "Compras"): a new etag on every change, <c>updated</c> now unless the task says when.</summary>
    private sealed class FakeGoogleTasks : IGoogleTasksClient
    {
        private readonly Dictionary<string, GoogleTask> _tasks = [];
        private int _version;

        public IEnumerable<GoogleTask> All => _tasks.Values.Where(t => !t.IsDeleted);

        public GoogleTask? Find(string id) => _tasks.GetValueOrDefault(id);

        public GoogleTask Put(GoogleTask task)
        {
            task = task with { Etag = $"\"{++_version}\"", Updated = task.Updated ?? DateTimeOffset.UtcNow };
            _tasks[task.Id] = task;
            return task;
        }

        public void Delete(string id) => Put(_tasks[id] with { Deleted = true, Updated = null });

        public Task<Result<IReadOnlyList<GoogleTaskListEntry>>> ListTaskListsAsync(Guid userId, Guid accountId, CancellationToken ct = default) =>
            Task.FromResult(Result<IReadOnlyList<GoogleTaskListEntry>>.Success([new GoogleTaskListEntry("L1", "Compras")]));

        public Task<Result<IReadOnlyList<GoogleTask>>> ListTasksAsync(
            Guid userId, Guid accountId, string listId, DateTimeOffset? updatedMin, CancellationToken ct = default)
        {
            IReadOnlyList<GoogleTask> tasks = updatedMin is { } min
                ? [.. _tasks.Values.Where(t => t.Updated >= min)]
                : [.. _tasks.Values.Where(t => !t.IsDeleted)];
            return Task.FromResult(Result<IReadOnlyList<GoogleTask>>.Success(tasks));
        }

        public Task<Result<GoogleTask>> GetTaskAsync(Guid userId, Guid accountId, string listId, string taskId, CancellationToken ct = default) =>
            Task.FromResult(_tasks.TryGetValue(taskId, out var task)
                ? Result<GoogleTask>.Success(task)
                : Result<GoogleTask>.Failure(GoogleTasksErrors.TaskNotFound));

        public Task<Result<GoogleTask>> InsertTaskAsync(
            Guid userId, Guid accountId, string listId, string? parentId, GoogleTaskWrite body, CancellationToken ct = default) =>
            Task.FromResult(Result<GoogleTask>.Success(Put(new GoogleTask(
                $"g{_version + 1}", null, body.Title, body.Notes, body.Status, body.Due, body.Completed, parentId, null, null))));

        public Task<Result<GoogleTask>> UpdateTaskAsync(
            Guid userId, Guid accountId, string listId, string taskId, GoogleTaskWrite body, CancellationToken ct = default) =>
            Task.FromResult(_tasks.TryGetValue(taskId, out var task) && !task.IsDeleted
                ? Result<GoogleTask>.Success(Put(task with
                {
                    Title = body.Title, Notes = body.Notes, Status = body.Status, Due = body.Due, Completed = body.Completed,
                    Updated = null
                }))
                : Result<GoogleTask>.Failure(GoogleTasksErrors.TaskNotFound));

        public Task<Result<bool>> DeleteTaskAsync(Guid userId, Guid accountId, string listId, string taskId, CancellationToken ct = default)
        {
            if (_tasks.ContainsKey(taskId))
                Delete(taskId);
            return Task.FromResult(Result<bool>.Success(true));
        }
    }
}
