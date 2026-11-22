# Calendar & Events

[← Back to index](../README.md) · Related: [Data Model](data-model.md), [Alerts & Sweep](alerts-and-sweep.md)

---

A **calendar** (`agd001`) is a named, colored container of events; a user has at least one `default`.
An **event** (`agd002`) occupies time — timed or all-day, single or recurring.

## 1. Occurrences are computed, never stored (D2)

A recurring event is **one row plus an RRULE**. Reads expand it in memory for the requested window
(`EventExpander`); nothing materializes a year of rows. Only *deviations* from the rule get stored, as
overrides. `recurrence_ends_at` (denormalized from UNTIL/COUNT) lets a range query prune finished
series by index instead of expanding every row.

- `starts_at` / `ends_at` are `timestamptz`; for an all-day event they are midnight in the event's
  `time_zone`, end **exclusive**.
- Recurrence expands in the event's own IANA `time_zone` (D4).
- `deleted_at` is a soft delete, so a future inbound sync can resurrect the event.

## 2. Overrides (`agd003`)

A single occurrence can deviate from its series, keyed by `(event_id, original_starts_at)`:

- **Cancelled** (`is_cancelled`) — the EXDATE case: the occurrence disappears from the grid.
- **Edited** — the non-null override columns (`starts_at`, `ends_at`, `title`, `description`,
  `location`) replace the series values for that one occurrence; NULL columns fall back to the series.

## 3. Edit scopes: this / this-and-future / all

Editing a recurring event offers three scopes:

| Scope | Effect |
|---|---|
| **this** | Writes an `agd003` override for the single occurrence. |
| **this and future** | **Splits** the series: the original event's recurrence is bounded, and a **new `agd002` row** carries the changed rule forward. No override is written. |
| **all** | Edits the `agd002` series row directly. |

This is why "a recurring event edited *this and future* splits correctly and the day view agrees."

## 4. Today

`GET /agenda/today` is the unified read that answers "what does my day look like" — the single screen
that justifies one module. It composes the day's events (expanded), due tasks and reminders into one
response (`GetToday`).

## 5. Commands & endpoints

Calendars: `CreateCalendar`, `UpdateCalendar`, `DeleteCalendar` (delete of a non-empty calendar
refused — archive instead). Events: `CreateEvent`, `UpdateEvent` (with edit scope), `DeleteEvent`.
HTTP: see [API Reference](api-reference.md) — `/agenda/calendars`, `/agenda/events` (the list endpoint
is the range query with in-memory expansion), `/agenda/today`.

## 6. Google Calendar

The user picks a Google account and some of its calendars in the Agenda settings; each becomes its own
Pandora calendar (`origin = External`), named and colored like the Google one, in its zone. A
**binding** (`agd009`) ties the two and holds Google's sync token; a **link** (`agd010`) ties each
Pandora event to its Google event. A calendar the account owns or may edit (`accessRole` `owner` or
`writer`) is bound `read-write`: edits go both ways. One it can only see is bound `read`.

**Cycle.** `GoogleCalendarSyncBackgroundService` runs one cycle per binding in `ok` every
`Pandora:Agenda:GoogleSyncInterval` (2 minutes); "Sync now" runs one at once. `GoogleCalendarSync`
first sends Pandora's edits (a `read-write` binding, see **Writing** below), then:

1. reads the calendar entry (its zone and default reminders) and the events, through
   `IGoogleCalendarClient` (REST, the account's token from Integrations);
2. lists everything from one year back when the binding has no token, after Google's 410, and every
   30 days; otherwise only the changes since the token, cancellations included;
3. fetches, for each series Pandora's recurrence engine cannot read (`BYSETPOS`, `RDATE`…), its
   instances over the next 12 months;
4. applies all of it in one unit of work, and stores the new token.

**Mapping** (`GoogleEventMapper`, a pure function tested against Google JSON):

| Google | Pandora |
|---|---|
| `summary`, `description`, `location` | title (`(sem título)` when empty), description, location |
| `hangoutLink` or the video entry point | `url` |
| `start`/`end` `dateTime`, or `date` for all-day | instants; all-day runs midnight to the exclusive next midnight in the calendar's zone |
| `status` `tentative` / `cancelled` | `Tentative` / soft delete |
| `RRULE:` line | `rrule` |
| `EXDATE` lines | cancelled overrides (`agd003`) |
| An exception (`recurringEventId` + `originalStartTime`) | an override of that occurrence (edited or cancelled) |
| `reminders.overrides`, or the calendar's `defaultReminders` | alerts (`agd007`), `offset = -minutes` |

A series Pandora cannot expand arrives as single events, one per instance, each linked with the
series' id (`series_remote_id`). Alerts are rewritten only when the event's reminders change in Google
(compared by hash), so an alert the user removes here stays removed until then.

A Google change to an event whose link is still pending is left for the push to settle (see
conflicts below), and a change whose `etag` equals the link's is Pandora's own coming back: neither
overwrites anything.

**Writing** (`read-write` bindings, `GoogleCalendarPush`). Every event write in a bound calendar —
create, edit at any scope, delete, and adding or removing an alert — marks the event's link pending
(`pending_push_at`) in the same transaction; a new event gets its link there, with a Google id Pandora
picks, so a retried insert finds its own event instead of duplicating it. The next cycle sends each
pending event:

| Pandora | Google |
|---|---|
| a new event | `insert` with Pandora's id |
| an edited event | `patch` with `If-Match` on the last etag: only the fields Pandora holds, so attendees and the meeting stay |
| a deleted event | `delete` (already gone counts as done) |
| "this and future" | the original series patched with its `UNTIL`, the tail inserted |
| an edited or cancelled occurrence | a patch or delete of the instance `<series id>_<original start>` |
| alerts | `popup` reminders: the five nearest before the start, up to four weeks |

The answer's etag goes on the link, which is how the reading half recognises the change. What fails
stays pending for the next cycle, and the binding shows the message without stopping.

**Conflicts.** Google's 412 on a patch means the event changed there too. The newer edit wins: Google's
`updated` against the time of Pandora's last edit. Pandora winning patches again without `If-Match`;
Google winning resets the cursor, so the same cycle lists everything and brings Google's version over
Pandora's. The losing version goes to `agd011` as JSON and to the conflict list in the settings.

**What stays fixed.** An event cannot move into or out of a Google calendar
(`Agenda.EventMoveAcrossGoogle`). In a `read` binding, creating, editing or deleting an event answers
`Agenda.CalendarReadOnly`, and the screens point to Google. A bound calendar cannot be deleted
(`Agenda.CalendarBound`); unlink it first.

**Failures.** A refused or unrefreshable token, or one without a calendar scope, puts the binding in
`needs-reconnect`; any other failure counts, and five in a row put it in `error`. Neither runs again
until "Sync now". `ExternalAccountRevoked` puts the account's bindings in `needs-reconnect` (the user is
told by Integrations' own revocation notice); `ExternalAccountDisconnected` unlinks them.

**Unlink** removes the binding and its links and turns the calendar back into a local one: the events
stay and become editable. Nothing changes in Google. The connections screen lists each binding as a use
of the account ("Agenda · calendar name").
