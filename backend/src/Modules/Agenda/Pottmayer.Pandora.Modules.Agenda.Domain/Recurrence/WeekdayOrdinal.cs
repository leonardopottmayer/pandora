namespace Pottmayer.Pandora.Modules.Agenda.Domain.Recurrence;

/// <summary>
/// One <c>BYDAY</c> entry: a weekday with an optional ordinal. <c>2TU</c> is the second Tuesday,
/// <c>-1FR</c> the last Friday, plain <c>MO</c> every Monday. Ordinals are only meaningful for
/// <see cref="RecurrenceFrequency.Monthly"/> and <see cref="RecurrenceFrequency.Yearly"/>.
/// </summary>
public readonly record struct WeekdayOrdinal(int? Ordinal, DayOfWeek Day);
