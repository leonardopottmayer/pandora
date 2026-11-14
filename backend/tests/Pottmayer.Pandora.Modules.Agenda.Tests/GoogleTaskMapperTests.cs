using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Google;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Agenda.Tests;

/// <summary>Pandora tasks ⇄ Google Tasks JSON: Google keeps a due date only, and done/not done.</summary>
public sealed class GoogleTaskMapperTests
{
    private const string Zone = "America/Sao_Paulo";

    private static GoogleTask Parse(string json) =>
        JsonSerializer.Deserialize<GoogleTask>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private static TaskItem Task(DateTimeOffset? due, bool hasTime) =>
        TaskItem.Create(Guid.NewGuid(), Guid.NewGuid(), "Ligar", "nota", due, hasTime, TaskPriority.High, Zone, null, 0, TimeProvider.System);

    [Fact]
    public void A_task_goes_with_its_due_date_in_its_own_zone_and_its_status()
    {
        // 22:00 on the 20th in São Paulo is already the 21st in UTC.
        var task = Task(new DateTimeOffset(2026, 11, 21, 1, 0, 0, TimeSpan.Zero), hasTime: true);

        var open = GoogleTaskMapper.ToGoogle(task, "abc");
        Assert.Equal("abc", open.Id);
        Assert.Equal("2026-11-20T00:00:00.000Z", open.Due);
        Assert.Equal("needsAction", open.Status);
        Assert.Null(open.Completed);
        Assert.Equal("nota", open.Notes);

        task.Complete(TimeProvider.System);
        var done = GoogleTaskMapper.ToGoogle(task, "abc");
        Assert.Equal("completed", done.Status);
        Assert.NotNull(done.Completed);

        Assert.Null(GoogleTaskMapper.ToGoogle(Task(null, false), null).Due);
    }

    [Fact]
    public void The_same_date_from_google_keeps_pandoras_time_and_a_new_one_is_a_whole_day()
    {
        var task = Task(new DateTimeOffset(2026, 11, 20, 18, 30, 0, TimeSpan.Zero), hasTime: true);
        var google = Parse("""
            { "kind": "tasks#task", "id": "t1", "etag": "\"e1\"", "title": "Ligar", "status": "needsAction",
              "due": "2026-11-20T00:00:00.000Z", "updated": "2026-11-13T12:00:00.000Z" }
            """);

        Assert.Equal((task.DueAt, true), GoogleTaskMapper.Due(google, task, Zone));

        var moved = GoogleTaskMapper.Due(google with { Due = "2026-11-22T00:00:00.000Z" }, task, Zone);
        Assert.False(moved.DueHasTime);
        Assert.Equal(new DateTimeOffset(2026, 11, 22, 15, 0, 0, TimeSpan.Zero), moved.DueAt); // noon in São Paulo

        Assert.Equal((null, false), GoogleTaskMapper.Due(google with { Due = null }, task, Zone));
    }

    [Fact]
    public void An_empty_title_is_shown_as_untitled_and_completed_and_deleted_are_read()
    {
        var google = Parse("""{ "id": "t1", "title": "", "status": "completed", "deleted": true }""");

        Assert.Equal(GoogleEventMapper.UntitledTitle, GoogleTaskMapper.Title(google));
        Assert.True(google.IsCompleted);
        Assert.True(google.IsDeleted);
    }
}
