using System.Globalization;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Google;

/// <summary>
/// Pandora task ⇄ Google task. Google Tasks keeps a title, notes, a due <i>date</i> and done/not done;
/// the due time, priority, alerts and recurrence stay in Pandora only, and reading never clears them.
/// </summary>
public static class GoogleTaskMapper
{
    public const string Completed = "completed";
    public const string NeedsAction = "needsAction";

    /// <summary>
    /// The task Pandora sends. A due date goes as the date in the task's zone (Google drops any time);
    /// done and cancelled both go as completed, Google having nothing else.
    /// </summary>
    public static GoogleTaskWrite ToGoogle(TaskItem task, string? id)
    {
        var done = task.Status is TaskItemStatus.Done or TaskItemStatus.Cancelled;
        return new GoogleTaskWrite(
            id,
            task.Title,
            task.Notes,
            done ? Completed : NeedsAction,
            DueDate(task) is { } date ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00:00.000Z" : null,
            done && task.CompletedAt is { } at ? at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture) : null);
    }

    /// <summary>The title Pandora shows: Google allows an empty one.</summary>
    public static string Title(GoogleTask google) =>
        string.IsNullOrWhiteSpace(google.Title) ? GoogleEventMapper.UntitledTitle : google.Title;

    /// <summary>
    /// The due Pandora keeps when Google says <paramref name="google"/>. The same date as Pandora's keeps
    /// Pandora's time of day; a new date is a whole day, at noon in <paramref name="zone"/> so the date
    /// reads the same a few hours either side.
    /// </summary>
    public static (DateTimeOffset? DueAt, bool DueHasTime) Due(GoogleTask google, TaskItem? current, string zone)
    {
        if (string.IsNullOrWhiteSpace(google.Due)
            || !DateTimeOffset.TryParse(google.Due, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return (null, false);

        var date = DateOnly.FromDateTime(parsed.UtcDateTime);
        if (current is not null && DueDate(current) == date)
            return (current.DueAt, current.DueHasTime);

        var tz = TimeZoneInfo.FindSystemTimeZoneById(zone);
        var noon = date.ToDateTime(new TimeOnly(12, 0));
        return (new DateTimeOffset(noon, tz.GetUtcOffset(noon)).ToUniversalTime(), false);
    }

    /// <summary>The task's due date in its own zone, or null.</summary>
    public static DateOnly? DueDate(TaskItem task) =>
        task.DueAt is { } due ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(due, task.ResolveZone()).DateTime) : null;
}
