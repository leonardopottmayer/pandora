using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.Recurrence;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Agenda.Tests;

/// <summary>
/// The pure core of the event-alert sweep: which occurrence anchors are due to fire. The per-anchor
/// ledger idempotency is proven end to end in the integration suite.
/// </summary>
public sealed class EventAlertTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Time = new FixedTimeProvider(Now);
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Cal = Guid.NewGuid();

    private static Event Make(DateTimeOffset startsAt, string? rrule = null) =>
        Event.Create(User, Cal, "Sync", null, null, null, startsAt, startsAt.AddHours(1),
            false, "UTC", rrule, EventStatus.Confirmed, Time);

    [Fact]
    public void A_single_event_with_a_zero_offset_fires_when_its_start_is_in_the_window()
    {
        var start = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var ev = Make(start);
        var now = start;
        var windowStart = start.AddMinutes(-5);

        var due = EventAlertExpansion.DueOccurrences(ev, offsetMinutes: 0, [], windowStart, now);

        Assert.Equal([start], due);
    }

    [Fact]
    public void A_start_outside_the_window_does_not_fire()
    {
        var start = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var ev = Make(start);
        // The window sits entirely before the start.
        var due = EventAlertExpansion.DueOccurrences(ev, 0, [], start.AddMinutes(-20), start.AddMinutes(-10));
        Assert.Empty(due);
    }

    [Fact]
    public void A_negative_offset_fires_ahead_of_the_start()
    {
        var start = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var ev = Make(start);
        // Alert 60 min before: firing instant is 08:00. A window around 08:00 fires; the anchor is the start.
        var firing = start.AddMinutes(-60);
        var due = EventAlertExpansion.DueOccurrences(ev, -60, [], firing.AddMinutes(-5), firing);

        Assert.Equal([start], due);
    }

    [Fact]
    public void A_recurring_event_fires_one_anchor_per_window()
    {
        var start = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var ev = Make(start, "FREQ=DAILY");
        var day2 = start.AddDays(1);

        var due = EventAlertExpansion.DueOccurrences(ev, 0, [], day2.AddMinutes(-5), day2);

        Assert.Equal([day2], due);
    }

    [Fact]
    public void A_cancelled_occurrence_does_not_fire()
    {
        var start = new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);
        var ev = Make(start, "FREQ=DAILY");
        var day2 = start.AddDays(1);
        var ov = EventOccurrenceOverride.Create(ev.Id, User, day2, Time);
        ov.Cancel();

        var due = EventAlertExpansion.DueOccurrences(ev, 0, [ov], day2.AddMinutes(-5), day2);

        Assert.Empty(due);
    }
}
