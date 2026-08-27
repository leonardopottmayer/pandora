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
