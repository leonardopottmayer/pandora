# API Reference

[← Back to index](../README.md) · Related: [Reminders](reminders.md), [Tasks](tasks.md), [Calendar & Events](calendar-and-events.md)

Base path: **`/api/v{version}/agenda`**. All endpoints are authenticated and scoped to the token's
user; another user's resource returns 404. Errors are mapped from typed `Result` failures.

---

## Reminders — `/agenda/reminders`

| Method | Path | Purpose |
|---|---|---|
| GET | `/agenda/reminders` | List the user's reminders. |
| POST | `/agenda/reminders` | Create a reminder (single-shot or recurring). |
| PATCH | `/agenda/reminders/{id}` | Rename; time, recurrence and status are untouched. |
| POST | `/agenda/reminders/{id}/acknowledge` | Acknowledge (single-shot). |
| POST | `/agenda/reminders/{id}/snooze` | Snooze (single-shot). |
| DELETE | `/agenda/reminders/{id}` | Cancel. |

Per-occurrence acknowledge/snooze for recurring reminders are `AcknowledgeOccurrence` /
`SnoozeOccurrence` commands (driven from the Telegram button path).

## Task lists — `/agenda/task-lists`

| Method | Path | Purpose |
|---|---|---|
| GET | `/agenda/task-lists` | List. |
| POST | `/agenda/task-lists` | Create. |
| PATCH | `/agenda/task-lists/{id}` | Rename / reorder / set default. |
| DELETE | `/agenda/task-lists/{id}` | Delete (refused if non-empty — archive instead). |

## Tasks — `/agenda/tasks`

| Method | Path | Purpose |
|---|---|---|
| GET | `/agenda/tasks` | List (by list/status). |
| POST | `/agenda/tasks` | Create (with optional parent, due, recurrence). |
| PATCH | `/agenda/tasks/{id}` | Update. |
| POST | `/agenda/tasks/{id}/complete` | Complete (recurring ⇒ next instance materialized). |
| POST | `/agenda/tasks/{id}/reopen` | Reopen. |
| DELETE | `/agenda/tasks/{id}` | Delete (cascades subtasks). |

## Calendars — `/agenda/calendars`

| Method | Path | Purpose |
|---|---|---|
| GET | `/agenda/calendars` | List. |
| POST | `/agenda/calendars` | Create. |
| PATCH | `/agenda/calendars/{id}` | Update (name/color/default/visibility). |
| DELETE | `/agenda/calendars/{id}` | Delete (refused if it has live events). |

## Events — `/agenda/events`

| Method | Path | Purpose |
|---|---|---|
| GET | `/agenda/events` | Range query — occurrences expanded in memory for the window. |
| GET | `/agenda/events/{id}` | One event. |
| POST | `/agenda/events` | Create (with optional recurrence). |
| PATCH | `/agenda/events/{id}` | Update with an edit scope (this / this-and-future / all). |
| DELETE | `/agenda/events/{id}` | Soft delete. |

## Alerts — `/agenda/{subjectType}/{id}/alerts`

| Method | Path | Purpose |
|---|---|---|
| GET | `/agenda/{subjectType}/{id}/alerts` | List a subject's alerts. |
| POST | `/agenda/{subjectType}/{id}/alerts` | Add an alert (offset + channels). |
| DELETE | `/agenda/alerts/{id}` | Remove an alert. |

`subjectType` is `task` or `event` (case-insensitive); `reminder` is rejected — reminders keep their own `agd006x` ledger instead of `Alert`.

## Preferences — `/agenda/preferences`

| Method | Path | Purpose |
|---|---|---|
| GET | `/agenda/preferences` | The user's Agenda preferences; the defaults when never saved. |
| PUT | `/agenda/preferences` | Save them: `{ "defaultAlertOffsetMinutes": -15 }`. |

## Today — `/agenda/today`

| Method | Path | Purpose |
|---|---|---|
| GET | `/agenda/today` | The unified day read: events (expanded) + due tasks + reminders. |

## Google Calendar — `/agenda/google`

| Method | Path | Purpose |
|---|---|---|
| GET | `/agenda/google/calendars?accountId=` | The calendars a connected Google account can see, each with the `bindingId` that already brings it, if any. |
| GET | `/agenda/google/bindings` | The user's Google calendar links: calendar, account, `direction` (`read`, `read-write`), status (`ok`, `needs-reconnect`, `error`), last sync, last error. |
| GET | `/agenda/google/conflicts` | The latest 50 events both sides changed: title, `losingSide` (`pandora`, `google`), `losingVersion` (JSON), `detectedAt`. |
| POST | `/agenda/google/bindings` | `{ accountId, calendarIds }`: one Pandora calendar per Google calendar. A calendar already linked is skipped. |
| POST | `/agenda/google/bindings/{id}/sync` | Sync now, resuming a stopped link; returns the link afterwards. |
| DELETE | `/agenda/google/bindings/{id}` | Unlink: the calendar and its events stay, as a local calendar. |

A calendar's `isReadOnly` (in `/agenda/calendars`) is true for a `read` link: its event writes answer
`Agenda.CalendarReadOnly` (409). Moving an event into or out of a linked calendar answers
`Agenda.EventMoveAcrossGoogle` (409), and deleting a linked calendar `Agenda.CalendarBound` (409).