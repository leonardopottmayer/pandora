using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Recurrence;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;

namespace Pottmayer.Pandora.Modules.Agenda.Application.Google;

/// <summary>What a Google event becomes in Pandora, before it touches an aggregate.</summary>
public sealed record MappedEvent(
    string Title,
    string? Description,
    string? Location,
    string? Url,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsAllDay,
    string TimeZone,
    string? Rrule,
    IReadOnlyList<DateTimeOffset> ExcludedStarts,
    EventStatus Status);

/// <summary>
/// Google event ↔ Pandora event, as pure functions: no clock, no I/O, so they are tested against real
/// Google JSON. All-day dates become midnights in the event's zone with an exclusive end, like Pandora's
/// own all-day events; <c>EXDATE</c> lines become the starts of cancelled occurrences.
/// </summary>
public static class GoogleEventMapper
{
    public const string UntitledTitle = "(sem título)";

    /// <summary>
    /// Maps a series or a single event. <paramref name="calendarZone"/> is the calendar's IANA zone, used
    /// when the event names none (always, for an all-day event).
    /// </summary>
    public static MappedEvent Map(GoogleEvent google, string calendarZone)
    {
        var zone = ZoneOf(google.Start?.TimeZone, calendarZone);
        var isAllDay = google.Start?.Date is not null;
        var startsAt = ToInstant(google.Start, zone) ?? throw new FormatException($"Google event {google.Id} has no start.");
        var endsAt = ToInstant(google.End, zone) ?? startsAt;
        var (rrule, excluded) = ReadRecurrence(google.Recurrence, zone);

        return new MappedEvent(
            string.IsNullOrWhiteSpace(google.Summary) ? UntitledTitle : google.Summary.Trim(),
            google.Description,
            google.Location,
            MeetingUrl(google),
            startsAt,
            endsAt < startsAt ? startsAt : endsAt,
            isAllDay,
            zone,
            rrule,
            excluded,
            string.Equals(google.Status, "tentative", StringComparison.OrdinalIgnoreCase) ? EventStatus.Tentative : EventStatus.Confirmed);
    }

    /// <summary>
    /// Whether Pandora's recurrence engine reads this series. A series it cannot read (an RRULE part
    /// outside the subset, an <c>RDATE</c>, an <c>EXRULE</c>) is brought as its instances instead.
    /// </summary>
    public static bool IsRecurrenceSupported(IReadOnlyList<string>? recurrence)
    {
        if (recurrence is not { Count: > 0 })
            return true;

        var rules = 0;
        foreach (var line in recurrence)
        {
            var name = line.Split([':', ';'], 2)[0].Trim().ToUpperInvariant();
            switch (name)
            {
                case "RRULE":
                    rules++;
                    try
                    {
                        RecurrenceRule.Parse(line);
                    }
                    catch (FormatException)
                    {
                        return false;
                    }
                    break;
                case "EXDATE":
                    break;
                default:
                    return false;
            }
        }

        return rules == 1;
    }

    /// <summary>The on-grid start of the occurrence an exception replaces, in UTC.</summary>
    public static DateTimeOffset? OriginalStart(GoogleEvent google, string calendarZone) =>
        ToInstant(google.OriginalStartTime, ZoneOf(google.OriginalStartTime?.TimeZone ?? google.Start?.TimeZone, calendarZone));

    /// <summary>
    /// The alerts the event asks for, as Pandora offsets (negative minutes, before the start): its own
    /// reminders, or the calendar's defaults when it uses them. Distinct and ordered.
    /// </summary>
    public static IReadOnlyList<int> AlertOffsets(GoogleEvent google, IReadOnlyList<GoogleReminder>? calendarDefaults)
    {
        var reminders = google.Reminders is null || google.Reminders.UseDefault
            ? calendarDefaults ?? []
            : google.Reminders.Overrides ?? [];

        return reminders.Select(r => -Math.Max(0, r.Minutes)).Distinct().Order().ToList();
    }

    /// <summary>A short stable hash of a set of offsets, to tell whether the alerts must be rewritten.</summary>
    public static string HashOffsets(IReadOnlyList<int> offsets)
    {
        var text = string.Join(',', offsets.Select(o => o.ToString(CultureInfo.InvariantCulture)));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];
    }

    /// <summary>Google keeps at most this many reminder overrides on an event.</summary>
    public const int MaxReminders = 5;

    /// <summary>Google's longest reminder: four weeks.</summary>
    private const int MaxReminderMinutes = 40320;

    /// <summary>
    /// Pandora event → what Google gets. An instance (one occurrence of a series Pandora holds as single
    /// events) sends no recurrence; a single event sends an empty one, so a rule removed in Pandora goes
    /// away in Google too. The meeting link stays Google's: Google has no plain field for it.
    /// </summary>
    public static GoogleEventWrite ToGoogle(Event ev, IEnumerable<int> alertOffsets, string? id, bool isInstance) => new(
        id,
        ev.Status == EventStatus.Tentative ? "tentative" : ev.Status == EventStatus.Cancelled ? "cancelled" : "confirmed",
        ev.Title,
        ev.Description ?? string.Empty,
        ev.Location ?? string.Empty,
        TimeOf(ev.StartsAt, ev.IsAllDay, ev.TimeZone),
        TimeOf(ev.EndsAt, ev.IsAllDay, ev.TimeZone),
        isInstance ? null
            : ev.Rrule is null ? []
            : [ev.Rrule.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase) ? ev.Rrule : "RRULE:" + ev.Rrule],
        Reminders(alertOffsets));

    /// <summary>One edited occurrence of a series, as a patch of its Google instance. A null field takes the series'.</summary>
    public static GoogleEventWrite ToGoogle(EventOccurrenceOverride occurrence, Event series)
    {
        var start = occurrence.StartsAt ?? occurrence.OriginalStartsAt;
        var end = occurrence.EndsAt ?? start + series.Duration;
        return new GoogleEventWrite(
            null, null,
            occurrence.Title ?? series.Title,
            occurrence.Description ?? series.Description ?? string.Empty,
            occurrence.Location ?? series.Location ?? string.Empty,
            TimeOf(start, series.IsAllDay, series.TimeZone),
            TimeOf(end, series.IsAllDay, series.TimeZone),
            null, null);
    }

    /// <summary>
    /// Google's id for one occurrence of a series: the series id, <c>_</c>, and the original start — the
    /// local date for an all-day series, the UTC instant otherwise.
    /// </summary>
    public static string InstanceId(string seriesRemoteId, Event series, DateTimeOffset originalStart) =>
        series.IsAllDay
            ? $"{seriesRemoteId}_{TimeZoneInfo.ConvertTime(originalStart, series.ResolveZone()):yyyyMMdd}"
            : $"{seriesRemoteId}_{originalStart.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Pandora's alerts as Google popup reminders: only those before the start (Google has no "after"),
    /// up to four weeks, the <see cref="MaxReminders"/> nearest.
    /// </summary>
    public static GoogleEventReminders Reminders(IEnumerable<int> alertOffsets) => new(
        false,
        alertOffsets.Where(o => o <= 0).Select(o => -o).Where(m => m <= MaxReminderMinutes)
            .Distinct().Order().Take(MaxReminders)
            .Select(m => new GoogleReminder("popup", m)).ToList());

    /// <summary>The hash <see cref="HashOffsets"/> gives the alerts these reminders come back as.</summary>
    public static string HashReminders(GoogleEventReminders reminders) =>
        HashOffsets((reminders.Overrides ?? []).Select(r => -Math.Max(0, r.Minutes)).Distinct().Order().ToList());

    private static GoogleEventTime TimeOf(DateTimeOffset instant, bool isAllDay, string zone) =>
        isAllDay
            ? new GoogleEventTime(
                TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(zone)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                null, null)
            : new GoogleEventTime(null, instant.ToUniversalTime(), zone);

    private static string? MeetingUrl(GoogleEvent google) =>
        google.HangoutLink
        ?? google.ConferenceData?.EntryPoints?.FirstOrDefault(p => p.EntryPointType == "video")?.Uri;

    private static (string? Rrule, IReadOnlyList<DateTimeOffset> Excluded) ReadRecurrence(IReadOnlyList<string>? recurrence, string zone)
    {
        if (recurrence is not { Count: > 0 })
            return (null, []);

        string? rrule = null;
        var excluded = new List<DateTimeOffset>();
        foreach (var line in recurrence)
        {
            if (line.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase))
                rrule = line["RRULE:".Length..].Trim();
            else if (line.StartsWith("EXDATE", StringComparison.OrdinalIgnoreCase))
                excluded.AddRange(ParseDateList(line, zone));
        }

        return (rrule, excluded);
    }

    /// <summary>
    /// <c>EXDATE;TZID=America/Sao_Paulo:20261110T090000,20261117T090000</c>,
    /// <c>EXDATE;VALUE=DATE:20261110</c> or <c>EXDATE:20261110T120000Z</c>, each value to a UTC instant.
    /// </summary>
    public static IEnumerable<DateTimeOffset> ParseDateList(string line, string defaultZone)
    {
        var colon = line.IndexOf(':');
        if (colon < 0)
            yield break;

        var zone = defaultZone;
        foreach (var parameter in line[..colon].Split(';').Skip(1))
        {
            var pair = parameter.Split('=', 2);
            if (pair.Length == 2 && pair[0].Equals("TZID", StringComparison.OrdinalIgnoreCase))
                zone = ZoneOf(pair[1], defaultZone);
        }

        foreach (var value in line[(colon + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (value.EndsWith('Z')
                && DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc))
                yield return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
            else if (DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
                yield return FromLocal(local, zone);
            else if (DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                yield return FromLocal(date.Date, zone);
        }
    }

    private static DateTimeOffset? ToInstant(GoogleEventTime? time, string zone)
    {
        if (time is null)
            return null;
        if (time.DateTime is { } instant)
            return instant.ToUniversalTime();
        if (time.Date is { } date
            && DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            return FromLocal(day.Date, zone);
        return null;
    }

    private static DateTimeOffset FromLocal(DateTime local, string zone)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(zone);
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        // A wall time skipped by a DST jump does not exist; the hour after it is the one that does.
        if (tz.IsInvalidTime(unspecified))
            unspecified = unspecified.AddHours(1);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, tz), TimeSpan.Zero);
    }

    /// <summary>The zone Google named, when this machine knows it; else the fallback; else UTC.</summary>
    private static string ZoneOf(string? candidate, string fallback)
    {
        foreach (var zone in new[] { candidate, fallback })
        {
            if (string.IsNullOrWhiteSpace(zone))
                continue;
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(zone);
                return zone;
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }

        return "UTC";
    }
}
