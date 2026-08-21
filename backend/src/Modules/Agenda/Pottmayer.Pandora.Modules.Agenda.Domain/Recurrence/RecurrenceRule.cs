using System.Globalization;

namespace Pottmayer.Pandora.Modules.Agenda.Domain.Recurrence;

/// <summary>
/// A pragmatic subset of an RFC 5545 <c>RRULE</c>, parsed from its raw string and expanded into
/// concrete occurrences. Pure and deterministic: it holds no clock and no series anchor. The anchor
/// (<c>DTSTART</c>) is passed to <see cref="Expand"/> and <see cref="ComputeEndsAt"/>, because in this
/// model the reminder owns the anchor (its <c>RemindAt</c>) and stores the rule beside it.
///
/// <para>Expansion is DST-aware by construction. Since the supported subset carries no
/// <c>BYHOUR/BYMINUTE/BYSECOND</c>, every occurrence keeps the anchor's local wall-clock time: the
/// engine enumerates qualifying local dates, pins the anchor's local time onto each, then converts to
/// UTC in the reminder's zone. "08:00 every weekday" stays 08:00 on the wall across a DST change even
/// though the UTC instant shifts.</para>
///
/// <para>Supported: <c>FREQ=DAILY|WEEKLY|MONTHLY|YEARLY</c>, <c>INTERVAL</c>, <c>BYDAY</c> (with
/// ordinals like <c>2TU</c>, <c>-1FR</c>), <c>BYMONTHDAY</c>, <c>BYMONTH</c>, <c>COUNT</c>,
/// <c>UNTIL</c>, <c>WKST</c>. Rejected on write: <c>BYSETPOS</c>, <c>BYWEEKNO</c>, <c>BYYEARDAY</c>,
/// <c>BYHOUR/BYMINUTE/BYSECOND</c>.</para>
/// </summary>
public sealed class RecurrenceRule
{
    // Guards a pathological rule (e.g. BYMONTHDAY=30 with FREQ=YEARLY;BYMONTH=2 never matching) from
    // spinning forever. Far beyond any real reminder horizon.
    private const int MaxCandidateDates = 200_000;

    private RecurrenceRule(
        RecurrenceFrequency frequency,
        int interval,
        IReadOnlyList<WeekdayOrdinal> byDay,
        IReadOnlyList<int> byMonthDay,
        IReadOnlyList<int> byMonth,
        int? count,
        DateTimeOffset? until,
        DayOfWeek weekStart,
        string raw)
    {
        Frequency = frequency;
        Interval = interval;
        ByDay = byDay;
        ByMonthDay = byMonthDay;
        ByMonth = byMonth;
        Count = count;
        Until = until;
        WeekStart = weekStart;
        Raw = raw;
    }

    public RecurrenceFrequency Frequency { get; }
    public int Interval { get; }
    public IReadOnlyList<WeekdayOrdinal> ByDay { get; }
    public IReadOnlyList<int> ByMonthDay { get; }
    public IReadOnlyList<int> ByMonth { get; }
    public int? Count { get; }
    public DateTimeOffset? Until { get; }
    public DayOfWeek WeekStart { get; }

    /// <summary>The original RRULE text, stored verbatim so a lossless copy round-trips to sync.</summary>
    public string Raw { get; }

    // ── Parsing ──

    /// <summary>
    /// Parses a raw RRULE string. Throws <see cref="FormatException"/> if it is malformed or uses a
    /// part outside the supported subset — that rejection is the write-time guard.
    /// </summary>
    public static RecurrenceRule Parse(string rrule)
    {
        if (string.IsNullOrWhiteSpace(rrule))
            throw new FormatException("An RRULE cannot be empty.");

        // Tolerate an "RRULE:" prefix; iCalendar wraps the parts in it.
        var body = rrule.Trim();
        if (body.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase))
            body = body["RRULE:".Length..];

        RecurrenceFrequency? frequency = null;
        var interval = 1;
        IReadOnlyList<WeekdayOrdinal> byDay = [];
        IReadOnlyList<int> byMonthDay = [];
        IReadOnlyList<int> byMonth = [];
        int? count = null;
        DateTimeOffset? until = null;
        var weekStart = DayOfWeek.Monday;

        foreach (var part in body.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2)
                throw new FormatException($"Malformed RRULE part '{part}'.");

            var name = kv[0].Trim().ToUpperInvariant();
            var value = kv[1].Trim();

            switch (name)
            {
                case "FREQ":
                    frequency = ParseFrequency(value);
                    break;
                case "INTERVAL":
                    interval = ParsePositiveInt(value, "INTERVAL");
                    break;
                case "BYDAY":
                    byDay = ParseByDay(value);
                    break;
                case "BYMONTHDAY":
                    byMonthDay = ParseByMonthDay(value);
                    break;
                case "BYMONTH":
                    byMonth = ParseByMonth(value);
                    break;
                case "COUNT":
                    count = ParsePositiveInt(value, "COUNT");
                    break;
                case "UNTIL":
                    until = ParseUntil(value);
                    break;
                case "WKST":
                    weekStart = ParseWeekday(value);
                    break;
                case "BYSETPOS":
                case "BYWEEKNO":
                case "BYYEARDAY":
                case "BYHOUR":
                case "BYMINUTE":
                case "BYSECOND":
                    throw new FormatException($"RRULE part '{name}' is not supported.");
                default:
                    throw new FormatException($"Unknown RRULE part '{name}'.");
            }
        }

        if (frequency is null)
            throw new FormatException("An RRULE must specify FREQ.");
        if (count is not null && until is not null)
            throw new FormatException("An RRULE cannot specify both COUNT and UNTIL.");

        // Ordinal BYDAY is only meaningful for MONTHLY/YEARLY (RFC 5545 §3.3.10).
        if (frequency is RecurrenceFrequency.Daily or RecurrenceFrequency.Weekly
            && byDay.Any(d => d.Ordinal is not null))
            throw new FormatException("Ordinal BYDAY (e.g. '2TU') requires FREQ=MONTHLY or YEARLY.");

        return new RecurrenceRule(
            frequency.Value, interval, byDay, byMonthDay, byMonth, count, until, weekStart, rrule.Trim());
    }

    /// <summary>Whether <paramref name="rrule"/> parses within the supported subset.</summary>
    public static bool IsValid(string rrule)
    {
        try
        {
            Parse(rrule);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static RecurrenceFrequency ParseFrequency(string value) => value.ToUpperInvariant() switch
    {
        "DAILY" => RecurrenceFrequency.Daily,
        "WEEKLY" => RecurrenceFrequency.Weekly,
        "MONTHLY" => RecurrenceFrequency.Monthly,
        "YEARLY" => RecurrenceFrequency.Yearly,
        _ => throw new FormatException($"Unsupported FREQ '{value}'."),
    };

    private static int ParsePositiveInt(string value, string part)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 1)
            throw new FormatException($"{part} must be a positive integer.");
        return n;
    }

    private static IReadOnlyList<WeekdayOrdinal> ParseByDay(string value)
    {
        var result = new List<WeekdayOrdinal>();
        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Length < 2)
                throw new FormatException($"Malformed BYDAY entry '{token}'.");

            var dayCode = token[^2..];
            var ordinalText = token[..^2];
            int? ordinal = null;
            if (ordinalText.Length > 0)
            {
                if (!int.TryParse(ordinalText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var ord) || ord == 0)
                    throw new FormatException($"Malformed BYDAY ordinal '{token}'.");
                ordinal = ord;
            }

            result.Add(new WeekdayOrdinal(ordinal, ParseWeekday(dayCode)));
        }

        if (result.Count == 0)
            throw new FormatException("BYDAY cannot be empty.");
        return result;
    }

    private static IReadOnlyList<int> ParseByMonthDay(string value)
    {
        var result = new List<int>();
        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)
                || n == 0 || n < -31 || n > 31)
                throw new FormatException($"BYMONTHDAY '{token}' must be a non-zero day between -31 and 31.");
            result.Add(n);
        }

        if (result.Count == 0)
            throw new FormatException("BYMONTHDAY cannot be empty.");
        return result;
    }

    private static IReadOnlyList<int> ParseByMonth(string value)
    {
        var result = new List<int>();
        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 1 || n > 12)
                throw new FormatException($"BYMONTH '{token}' must be between 1 and 12.");
            result.Add(n);
        }

        if (result.Count == 0)
            throw new FormatException("BYMONTH cannot be empty.");
        return result;
    }

    private static DateTimeOffset ParseUntil(string value)
    {
        // RFC 5545 UNTIL is normally a UTC datetime (…T…Z). We also accept a bare date (end of day).
        var formats = new[]
        {
            "yyyyMMdd'T'HHmmss'Z'",
            "yyyyMMdd'T'HHmmss",
            "yyyyMMdd",
        };

        if (!DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            throw new FormatException($"Malformed UNTIL '{value}'.");

        // A date-only UNTIL bounds the whole final day.
        if (value.Length == 8)
            parsed = parsed.Date.AddDays(1).AddTicks(-1);

        return new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Utc), TimeSpan.Zero);
    }

    private static DayOfWeek ParseWeekday(string code) => code.ToUpperInvariant() switch
    {
        "MO" => DayOfWeek.Monday,
        "TU" => DayOfWeek.Tuesday,
        "WE" => DayOfWeek.Wednesday,
        "TH" => DayOfWeek.Thursday,
        "FR" => DayOfWeek.Friday,
        "SA" => DayOfWeek.Saturday,
        "SU" => DayOfWeek.Sunday,
        _ => throw new FormatException($"Unknown weekday '{code}'."),
    };

    /// <summary>
    /// A copy of this rule's raw text bounded to end at <paramref name="until"/>: any existing
    /// <c>UNTIL</c> or <c>COUNT</c> part is dropped and replaced by <c>UNTIL=&lt;until, UTC basic
    /// format&gt;</c>. This is what the "this and future" split uses to end the original series just
    /// before the cut occurrence, leaving a new event to carry the tail.
    /// </summary>
    public string WithUntil(DateTimeOffset until)
    {
        var formatted = until.ToUniversalTime().UtcDateTime
            .ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

        var body = Raw.Trim();
        if (body.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase))
            body = body["RRULE:".Length..];

        var parts = body
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Split('=', 2)[0].Trim().ToUpperInvariant() is not "UNTIL" and not "COUNT")
            .Append($"UNTIL={formatted}");

        return string.Join(';', parts);
    }

    // ── Expansion ──

    /// <summary>
    /// Every occurrence instant that falls within <c>[from, to]</c> (both inclusive), in ascending
    /// order. <paramref name="seriesStart"/> is the anchor (DTSTART); occurrences never precede it,
    /// and <c>COUNT</c> is counted from it regardless of where the window sits.
    /// </summary>
    public IEnumerable<DateTimeOffset> Expand(
        DateTimeOffset seriesStart, DateTimeOffset from, DateTimeOffset to, TimeZoneInfo zone)
    {
        if (to < from)
            yield break;

        var localStart = TimeZoneInfo.ConvertTime(seriesStart, zone).DateTime;
        var timeOfDay = localStart.TimeOfDay;
        var startDate = DateOnly.FromDateTime(localStart);

        // Generation stops a day past the window end in local terms; a day's UTC instant can never
        // exceed the next local day's instant, so this cannot clip a valid occurrence.
        var stopDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(to, zone).DateTime).AddDays(1);

        var emitted = 0;
        foreach (var date in EnumerateDates(startDate, stopDate))
        {
            var instant = ToInstant(date, timeOfDay, zone);

            if (Until is { } until && instant > until)
                yield break;

            emitted++;
            if (instant >= from && instant <= to)
                yield return instant;

            if (Count is { } count && emitted >= count)
                yield break;
        }
    }

    /// <summary>
    /// The last occurrence instant of a bounded series (via <c>COUNT</c> or <c>UNTIL</c>), for the
    /// denormalized <c>recurrence_ends_at</c> that lets the sweep prune finished series by index.
    /// <see langword="null"/> for an unbounded rule.
    /// </summary>
    public DateTimeOffset? ComputeEndsAt(DateTimeOffset seriesStart, TimeZoneInfo zone)
    {
        if (Count is null && Until is null)
            return null;

        var localStart = TimeZoneInfo.ConvertTime(seriesStart, zone).DateTime;
        var timeOfDay = localStart.TimeOfDay;
        var startDate = DateOnly.FromDateTime(localStart);

        // For UNTIL, stop a day past it; for COUNT, run open-ended (guarded by MaxCandidateDates) and
        // let the count cut it off.
        var stopDate = Until is { } u
            ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(u, zone).DateTime).AddDays(1)
            : DateOnly.MaxValue;

        DateTimeOffset? last = null;
        var emitted = 0;
        foreach (var date in EnumerateDates(startDate, stopDate))
        {
            var instant = ToInstant(date, timeOfDay, zone);
            if (Until is { } until && instant > until)
                break;

            last = instant;
            emitted++;
            if (Count is { } count && emitted >= count)
                break;
        }

        return last;
    }

    // Qualifying local dates in ascending order, from startDate up to (and including) stopDate. The
    // anchor date supplies the defaults (its day-of-month, its weekday) when the rule names none.
    private IEnumerable<DateOnly> EnumerateDates(DateOnly startDate, DateOnly stopDate) => Frequency switch
    {
        RecurrenceFrequency.Daily => EnumerateDaily(startDate, stopDate),
        RecurrenceFrequency.Weekly => EnumerateWeekly(startDate, stopDate),
        RecurrenceFrequency.Monthly => EnumerateMonthly(startDate, stopDate),
        RecurrenceFrequency.Yearly => EnumerateYearly(startDate, stopDate),
        _ => [],
    };

    private IEnumerable<DateOnly> EnumerateDaily(DateOnly startDate, DateOnly stopDate)
    {
        var seen = 0;
        for (var date = startDate; date <= stopDate; date = date.AddDays(1))
        {
            if ((date.DayNumber - startDate.DayNumber) % Interval != 0)
                continue;
            if (!PassesMonthFilter(date) || !PassesMonthDayFilter(date) || !PassesSimpleDayFilter(date))
                continue;
            if (++seen > MaxCandidateDates)
                yield break;
            yield return date;
        }
    }

    private IEnumerable<DateOnly> EnumerateWeekly(DateOnly startDate, DateOnly stopDate)
    {
        var anchorWeekday = startDate.DayOfWeek;

        // Anchor the interval on the WKST-aligned week containing the start date.
        var offsetIntoWeek = ((int)startDate.DayOfWeek - (int)WeekStart + 7) % 7;
        var firstWeekStart = startDate.AddDays(-offsetIntoWeek);

        var seen = 0;
        for (var weekStart = firstWeekStart; weekStart <= stopDate; weekStart = weekStart.AddDays(7 * Interval))
        {
            for (var i = 0; i < 7; i++)
            {
                var date = weekStart.AddDays(i);
                if (date < startDate || date > stopDate)
                    continue;
                if (!PassesWeeklyDayFilter(date, anchorWeekday) || !PassesMonthFilter(date) || !PassesMonthDayFilter(date))
                    continue;
                if (++seen > MaxCandidateDates)
                    yield break;
                yield return date;
            }
        }
    }

    private IEnumerable<DateOnly> EnumerateMonthly(DateOnly startDate, DateOnly stopDate)
    {
        var seen = 0;
        var cursor = new DateOnly(startDate.Year, startDate.Month, 1);
        for (var step = 0; ; step++)
        {
            var month = cursor.AddMonths(step * Interval);
            if (month > stopDate)
                yield break;
            if (ByMonth.Count > 0 && !ByMonth.Contains(month.Month))
                continue;

            foreach (var date in DaysInMonth(month.Year, month.Month, startDate.Day))
            {
                if (date < startDate || date > stopDate)
                    continue;
                if (++seen > MaxCandidateDates)
                    yield break;
                yield return date;
            }
        }
    }

    private IEnumerable<DateOnly> EnumerateYearly(DateOnly startDate, DateOnly stopDate)
    {
        var seen = 0;
        var months = ByMonth.Count > 0 ? ByMonth.OrderBy(m => m).ToArray() : [startDate.Month];

        for (var step = 0; ; step++)
        {
            var year = startDate.Year + step * Interval;
            if (year > stopDate.Year)
                yield break;

            foreach (var month in months)
            {
                foreach (var date in DaysInMonth(year, month, startDate.Day))
                {
                    if (date < startDate || date > stopDate)
                        continue;
                    if (++seen > MaxCandidateDates)
                        yield break;
                    yield return date;
                }
            }
        }
    }

    // The qualifying days within one month, ascending. Combines BYMONTHDAY and BYDAY (their
    // intersection when both are given); falls back to the anchor's day-of-month when neither is set.
    private IEnumerable<DateOnly> DaysInMonth(int year, int month, int anchorDay)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);

        SortedSet<int>? fromMonthDay = null;
        if (ByMonthDay.Count > 0)
        {
            fromMonthDay = [];
            foreach (var md in ByMonthDay)
            {
                var day = md > 0 ? md : daysInMonth + md + 1;
                if (day >= 1 && day <= daysInMonth)
                    fromMonthDay.Add(day);
            }
        }

        SortedSet<int>? fromByDay = null;
        if (ByDay.Count > 0)
        {
            fromByDay = [];
            foreach (var day in ExpandByDayInMonth(year, month, daysInMonth))
                fromByDay.Add(day);
        }

        IEnumerable<int> days;
        if (fromMonthDay is not null && fromByDay is not null)
            days = fromMonthDay.Intersect(fromByDay).Order();
        else if (fromMonthDay is not null)
            days = fromMonthDay;
        else if (fromByDay is not null)
            days = fromByDay;
        else
            // No BYMONTHDAY/BYDAY: the anchor's day-of-month, skipped in months too short for it
            // (e.g. a day-31 series has no February occurrence).
            days = anchorDay <= daysInMonth ? [anchorDay] : [];

        foreach (var day in days)
            if (day >= 1 && day <= daysInMonth)
                yield return new DateOnly(year, month, day);
    }

    private IEnumerable<int> ExpandByDayInMonth(int year, int month, int daysInMonth)
    {
        foreach (var entry in ByDay)
        {
            // All matching weekdays in the month, 1-based positions.
            var matches = new List<int>();
            for (var day = 1; day <= daysInMonth; day++)
                if (new DateTime(year, month, day).DayOfWeek == entry.Day)
                    matches.Add(day);

            if (entry.Ordinal is null)
            {
                foreach (var day in matches)
                    yield return day;
            }
            else
            {
                var ord = entry.Ordinal.Value;
                var index = ord > 0 ? ord - 1 : matches.Count + ord;
                if (index >= 0 && index < matches.Count)
                    yield return matches[index];
            }
        }
    }

    private bool PassesMonthFilter(DateOnly date) => ByMonth.Count == 0 || ByMonth.Contains(date.Month);

    private bool PassesMonthDayFilter(DateOnly date)
    {
        if (ByMonthDay.Count == 0)
            return true;
        var daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);
        foreach (var md in ByMonthDay)
        {
            var day = md > 0 ? md : daysInMonth + md + 1;
            if (day == date.Day)
                return true;
        }
        return false;
    }

    // For DAILY: BYDAY is a plain weekday limit (ordinals rejected at parse).
    private bool PassesSimpleDayFilter(DateOnly date) =>
        ByDay.Count == 0 || ByDay.Any(d => d.Day == date.DayOfWeek);

    // For WEEKLY: BYDAY expands within the week; with none, the anchor's weekday is implied.
    private bool PassesWeeklyDayFilter(DateOnly date, DayOfWeek anchorWeekday) =>
        ByDay.Count == 0 ? date.DayOfWeek == anchorWeekday : ByDay.Any(d => d.Day == date.DayOfWeek);

    private DateTimeOffset ToInstant(DateOnly date, TimeSpan timeOfDay, TimeZoneInfo zone)
    {
        var local = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified)
            .Add(timeOfDay);

        // Spring-forward gap: the wall time does not exist; push past it. Fall-back ambiguity is left
        // to ConvertTimeToUtc, which resolves it to standard time. 08:00-style reminders never hit the
        // gap, so this only guards exotic anchors.
        if (zone.IsInvalidTime(local))
            local = local.AddHours(1);

        var utc = TimeZoneInfo.ConvertTimeToUtc(local, zone);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }
}
