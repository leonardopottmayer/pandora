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

`subjectType` is `tasks` today (the only wired subject).

## Today — `/agenda/today`

| Method | Path | Purpose |
|---|---|---|
| GET | `/agenda/today` | The unified day read: events (expanded) + due tasks + reminders. |
