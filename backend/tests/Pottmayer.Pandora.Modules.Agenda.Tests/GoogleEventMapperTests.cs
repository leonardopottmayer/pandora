using System.Text.Json;
using Pottmayer.Pandora.Modules.Agenda.Application.Google;
using Pottmayer.Pandora.Modules.Agenda.Domain.Aggregates;
using Pottmayer.Pandora.Modules.Agenda.Domain.ValueObjects;
using Xunit;

namespace Pottmayer.Pandora.Modules.Agenda.Tests;

/// <summary>Google Calendar JSON, in the shape the API returns it, mapped to Pandora's event fields.</summary>
public sealed class GoogleEventMapperTests
{
    private const string Zone = "America/Sao_Paulo";

    private static GoogleEvent Parse(string json) =>
        JsonSerializer.Deserialize<GoogleEvent>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public void A_timed_event_keeps_its_instants_zone_meeting_link_and_status()
    {
        var google = Parse("""
            {
              "kind": "calendar#event", "id": "abc123", "status": "tentative", "etag": "\"3456\"",
              "updated": "2026-11-09T12:00:00.000Z", "summary": " Dentista ", "location": "Rua X, 10",
              "hangoutLink": "https://meet.google.com/aaa-bbbb-ccc",
              "start": { "dateTime": "2026-11-10T09:00:00-03:00", "timeZone": "America/Sao_Paulo" },
              "end": { "dateTime": "2026-11-10T10:30:00-03:00", "timeZone": "America/Sao_Paulo" }
            }
            """);

        var mapped = GoogleEventMapper.Map(google, "UTC");

        Assert.Equal("Dentista", mapped.Title);
        Assert.Equal(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero), mapped.StartsAt);
        Assert.Equal(new DateTimeOffset(2026, 11, 10, 13, 30, 0, TimeSpan.Zero), mapped.EndsAt);
        Assert.False(mapped.IsAllDay);
        Assert.Equal(Zone, mapped.TimeZone);
        Assert.Equal("https://meet.google.com/aaa-bbbb-ccc", mapped.Url);
        Assert.Equal(EventStatus.Tentative, mapped.Status);
        Assert.Null(mapped.Rrule);
    }

    [Fact]
    public void An_all_day_event_runs_from_midnight_to_the_exclusive_next_midnight_in_the_calendar_zone()
    {
        var google = Parse("""
            { "id": "d1", "status": "confirmed", "summary": "Feriado",
              "start": { "date": "2026-11-15" }, "end": { "date": "2026-11-16" } }
            """);

        var mapped = GoogleEventMapper.Map(google, Zone);

        Assert.True(mapped.IsAllDay);
        Assert.Equal(Zone, mapped.TimeZone);
        Assert.Equal(new DateTimeOffset(2026, 11, 15, 3, 0, 0, TimeSpan.Zero), mapped.StartsAt);
        Assert.Equal(new DateTimeOffset(2026, 11, 16, 3, 0, 0, TimeSpan.Zero), mapped.EndsAt);
    }

    [Fact]
    public void A_series_keeps_its_rule_and_its_excluded_dates_as_utc_starts()
    {
        var google = Parse("""
            { "id": "s1", "status": "confirmed", "summary": "Daily",
              "start": { "dateTime": "2026-11-02T09:00:00-03:00", "timeZone": "America/Sao_Paulo" },
              "end": { "dateTime": "2026-11-02T09:15:00-03:00", "timeZone": "America/Sao_Paulo" },
              "recurrence": [
                "EXDATE;TZID=America/Sao_Paulo:20261104T090000,20261106T090000",
                "RRULE:FREQ=WEEKLY;BYDAY=MO,WE,FR;UNTIL=20261231T025959Z"
              ] }
            """);

        var mapped = GoogleEventMapper.Map(google, "UTC");

        Assert.Equal("FREQ=WEEKLY;BYDAY=MO,WE,FR;UNTIL=20261231T025959Z", mapped.Rrule);
        Assert.Equal(
            [new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero)],
            mapped.ExcludedStarts);
        Assert.True(GoogleEventMapper.IsRecurrenceSupported(google.Recurrence));
    }

    [Theory]
    [InlineData("EXDATE;VALUE=DATE:20261110", "2026-11-10T03:00:00Z")]
    [InlineData("EXDATE:20261110T120000Z", "2026-11-10T12:00:00Z")]
    [InlineData("EXDATE:20261110T090000", "2026-11-10T12:00:00Z")]
    public void Excluded_dates_read_every_form_google_writes(string line, string expected)
    {
        var start = Assert.Single(GoogleEventMapper.ParseDateList(line, Zone));
        Assert.Equal(DateTimeOffset.Parse(expected), start);
    }

    [Theory]
    [InlineData(new[] { "RRULE:FREQ=MONTHLY;BYDAY=MO;BYSETPOS=-1" }, false)]
    [InlineData(new[] { "RRULE:FREQ=WEEKLY", "RDATE;VALUE=DATE:20261225" }, false)]
    [InlineData(new[] { "RRULE:FREQ=MONTHLY;BYDAY=-1FR" }, true)]
    [InlineData(new string[0], true)]
    public void Only_rules_pandora_expands_count_as_supported(string[] recurrence, bool supported) =>
        Assert.Equal(supported, GoogleEventMapper.IsRecurrenceSupported(recurrence));

    [Fact]
    public void Reminders_become_offsets_before_the_start_from_the_event_or_the_calendar_defaults()
    {
        GoogleReminder[] defaults = [new("popup", 30), new("email", 1440)];
        var own = Parse("""
            { "id": "r1", "start": { "dateTime": "2026-11-10T09:00:00Z" },
              "reminders": { "useDefault": false, "overrides": [ { "method": "popup", "minutes": 10 }, { "method": "email", "minutes": 10 }, { "method": "popup", "minutes": 0 } ] } }
            """);
        var inherited = Parse("""{ "id": "r2", "start": { "dateTime": "2026-11-10T09:00:00Z" }, "reminders": { "useDefault": true } }""");
        var none = Parse("""{ "id": "r3", "start": { "dateTime": "2026-11-10T09:00:00Z" }, "reminders": { "useDefault": false } }""");

        Assert.Equal([-10, 0], GoogleEventMapper.AlertOffsets(own, defaults));
        Assert.Equal([-1440, -30], GoogleEventMapper.AlertOffsets(inherited, defaults));
        Assert.Empty(GoogleEventMapper.AlertOffsets(none, defaults));
        Assert.Equal(GoogleEventMapper.HashOffsets([-10, 0]), GoogleEventMapper.HashOffsets([-10, 0]));
        Assert.NotEqual(GoogleEventMapper.HashOffsets([-10]), GoogleEventMapper.HashOffsets([-30]));
    }

    [Fact]
    public void An_exception_names_the_occurrence_it_replaces_and_a_missing_title_gets_a_placeholder()
    {
        var exception = Parse("""
            { "id": "s1_20261109T120000Z", "status": "confirmed", "recurringEventId": "s1",
              "originalStartTime": { "dateTime": "2026-11-09T09:00:00-03:00", "timeZone": "America/Sao_Paulo" },
              "start": { "dateTime": "2026-11-09T10:00:00-03:00" }, "end": { "dateTime": "2026-11-09T10:15:00-03:00" } }
            """);

        Assert.Equal(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero), GoogleEventMapper.OriginalStart(exception, Zone));
        Assert.Equal(GoogleEventMapper.UntitledTitle, GoogleEventMapper.Map(exception, Zone).Title);
    }
    // ── Pandora → Google ──

    private static Event NewEvent(DateTimeOffset start, DateTimeOffset end, bool allDay, string? rrule = null) =>
        Event.Create(Guid.NewGuid(), Guid.NewGuid(), "Plantão", null, null, null, start, end, allDay, Zone, rrule,
            EventStatus.Confirmed, TimeProvider.System);

    [Fact]
    public void An_all_day_event_goes_as_local_dates_with_an_exclusive_end()
    {
        // 15 Nov in São Paulo is 03:00 UTC.
        var ev = NewEvent(new(2026, 11, 15, 3, 0, 0, TimeSpan.Zero), new(2026, 11, 16, 3, 0, 0, TimeSpan.Zero), allDay: true);

        var write = GoogleEventMapper.ToGoogle(ev, [], id: null, isInstance: false);

        Assert.Equal("2026-11-15", write.Start!.Date);
        Assert.Equal("2026-11-16", write.End!.Date);
        Assert.Null(write.Start.DateTime);
        Assert.Empty(write.Recurrence!); // no rule: a rule removed in Pandora goes away in Google too
        Assert.Equal("", write.Description);
    }

    [Fact]
    public void A_series_goes_with_its_rule_and_an_instance_without_one()
    {
        var ev = NewEvent(new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero), new(2026, 11, 2, 13, 0, 0, TimeSpan.Zero), false, "FREQ=WEEKLY;BYDAY=MO");

        Assert.Equal(["RRULE:FREQ=WEEKLY;BYDAY=MO"], GoogleEventMapper.ToGoogle(ev, [], null, isInstance: false).Recurrence);
        Assert.Null(GoogleEventMapper.ToGoogle(ev, [], null, isInstance: true).Recurrence);
        Assert.Equal(Zone, GoogleEventMapper.ToGoogle(ev, [], null, false).Start!.TimeZone);
    }

    [Fact]
    public void Instance_ids_follow_googles_shape()
    {
        var timed = NewEvent(new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero), new(2026, 11, 2, 13, 0, 0, TimeSpan.Zero), false, "FREQ=DAILY");
        var allDay = NewEvent(new(2026, 11, 2, 3, 0, 0, TimeSpan.Zero), new(2026, 11, 3, 3, 0, 0, TimeSpan.Zero), true, "FREQ=DAILY");

        Assert.Equal("s1_20261109T120000Z", GoogleEventMapper.InstanceId("s1", timed, new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero)));
        Assert.Equal("s1_20261109", GoogleEventMapper.InstanceId("s1", allDay, new(2026, 11, 9, 3, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Alerts_become_at_most_five_popup_reminders_before_the_start()
    {
        var reminders = GoogleEventMapper.Reminders([10, -5, -5, -60, -1440, -15, -30, -120, -50000]);

        Assert.False(reminders.UseDefault);
        Assert.Equal([5, 15, 30, 60, 120], reminders.Overrides!.Select(r => r.Minutes));
        Assert.All(reminders.Overrides!, r => Assert.Equal("popup", r.Method));
        // They come back from Google as the same alerts, so the hash matches the reading side's.
        Assert.Equal(GoogleEventMapper.HashOffsets([-120, -60, -30, -15, -5]), GoogleEventMapper.HashReminders(reminders));
    }
}