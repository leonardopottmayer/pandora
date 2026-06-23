namespace Pottmayer.Pandora.Modules.Finances.Domain.ValueObjects;

/// <summary>
/// Encapsulates recurrence rule logic: frequency, interval, optional day/weekday anchor,
/// start/end dates and max-occurrence limit. Computes the next occurrence after a given date
/// and determines whether the recurrence has terminated.
/// </summary>
public sealed class RecurrenceRule
{
    public RecurrenceFrequency Frequency { get; }
    public short Interval { get; }
    public short? DayOfMonth { get; }
    public short? Weekday { get; }
    public DateOnly StartDate { get; }
    public DateOnly? EndDate { get; }
    public int? MaxOccurrences { get; }

    private RecurrenceRule(RecurrenceFrequency frequency, short interval, short? dayOfMonth, short? weekday,
        DateOnly startDate, DateOnly? endDate, int? maxOccurrences)
    {
        Frequency = frequency;
        Interval = interval;
        DayOfMonth = dayOfMonth;
        Weekday = weekday;
        StartDate = startDate;
        EndDate = endDate;
        MaxOccurrences = maxOccurrences;
    }

    public static RecurrenceRule Create(
        RecurrenceFrequency frequency, short interval, short? dayOfMonth, short? weekday,
        DateOnly startDate, DateOnly? endDate, int? maxOccurrences) =>
        new(frequency, interval, dayOfMonth, weekday, startDate, endDate, maxOccurrences);

    /// <summary>
    /// Returns the next occurrence date after <paramref name="current"/>.
    /// For monthly/yearly, <see cref="DayOfMonth"/> is clamped to the last valid day of the target month
    /// (e.g. day 31 in February → 28/29).
    /// </summary>
    public DateOnly NextOccurrenceAfter(DateOnly current)
    {
        if (Frequency == RecurrenceFrequency.Daily) return current.AddDays(Interval);
        if (Frequency == RecurrenceFrequency.Weekly) return current.AddDays(Interval * 7);
        if (Frequency == RecurrenceFrequency.Monthly) return NextMonthly(current);
        if (Frequency == RecurrenceFrequency.Yearly) return NextYearly(current);
        throw new InvalidOperationException($"Unsupported frequency: {Frequency}");
    }

    /// <summary>
    /// Returns <c>true</c> when no further occurrences should be generated: the next proposed date
    /// exceeds the optional <see cref="EndDate"/>, or <paramref name="count"/> has reached
    /// <see cref="MaxOccurrences"/>.
    /// </summary>
    public bool IsTerminated(DateOnly nextDate, int count)
    {
        if (MaxOccurrences.HasValue && count >= MaxOccurrences.Value) return true;
        if (EndDate.HasValue && nextDate > EndDate.Value) return true;
        return false;
    }

    private DateOnly NextMonthly(DateOnly current)
    {
        var target = current.AddMonths(Interval);
        var day = DayOfMonth ?? current.Day;
        return Clamp(target.Year, target.Month, day);
    }

    private DateOnly NextYearly(DateOnly current)
    {
        var target = current.AddYears(Interval);
        var day = DayOfMonth ?? current.Day;
        return Clamp(target.Year, target.Month, day);
    }

    private static DateOnly Clamp(int year, int month, int day) =>
        new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
}
