# Implementation Status

[← Back to index](../README.md)

A snapshot of what is built versus what is designed but not yet implemented. The forward roadmap lives
in [product-plan.md](product-plan.md).

---

## Implemented (phases 1–4 + frontend)

| Area | Notes |
|---|---|
| **Module scaffold** | Seven layered projects; `agenda` schema; DI + module registration. |
| **Reminders** | `agd006` single-shot + recurring; `agd006x` per-occurrence ledger; acknowledge/snooze (series and per-occurrence); `ReminderSweepBackgroundService`. |
| **Recurrence engine** | `RecurrenceRule` parse + `EventExpander` expand, RFC 5545 subset, DST-aware, `-1FR`-style ordinals; expands in the item's `time_zone`. |
| **Tasks** | `agd004` lists + `agd005` tasks; one-level subtasks; priority; due with/without time; complete/reopen; recurring materialized one instance at a time carrying alerts. |
| **Alerts** | `agd007` polymorphic + `agd008` dispatch ledger; `Task` and `Event` subjects wired; `TaskAlertSweepBackgroundService` + `EventAlertSweepBackgroundService`. |
| **Calendar & events** | `agd001` calendars, `agd002` events (computed occurrences), `agd003` overrides; this / this-and-future / all edit scopes; `EventAlertSweepBackgroundService`. |
| **Today** | `GET /agenda/today` unified read (events + tasks + reminders). |
| **Inbound buttons** | `InboundInteractionReceivedHandler` + `TaskInteractionHandler` for `task_done` / `snooze_*` from Channels. |
| **API** | Reminders, task-lists, tasks, calendars, events, alerts, today controllers. |
| **Frontend** | `client-web/src/modules/agenda` — Today, Reminders, Tasks, Calendar. |

### Frontend form and loading polish

Event creation/editing, tasks and reminder forms use 640 px modals with scrolling bodies. Related
task fields pair on desktop and stack on phones. Optional notes, event details and recurrence
controls fold away; existing event details and task notes open when populated. Dates use
DD/MM/YYYY and time uses HH:mm. Event editing validates the calendar and period before submitting;
series-load failures disable editing/deletion rather than guessing recurrence scope.

Recurrence endings wrap as radio options; event edit scope uses a full-width select. Alert rows
use native lists and wrapping controls instead of deprecated Ant Design List/Space APIs. Snooze
refreshes its custom time on each opening or reminder change, using the user's time zone.
Today, Calendar, Tasks, Reminders and Settings distinguish request failures from empty results.
All existing request formats, recurrence rules and time-zone conversions are preserved.


### Notable deviations from the original plan

- **Three sweeps, not one.** `ReminderSweep`, `TaskAlertSweep`, `EventAlertSweep` replace the single
  `AlertSweepBackgroundService` — each subject type expands differently.
- **Reminder dispatch ledger is `agd006x`** (reminder-scoped), not the polymorphic `agd008`. Migrates
  to `agd008` when Alert covers reminders.
- **`Alert.subject_type` admits `Task`/`Event`/`Reminder`; `Task` and `Event` are wired** (create/list/
  sweep), **`Reminder` is not** — reminders keep their own `agd006x` ledger.
- **`time_zone` is carried per row** (reminder/task/event/calendar) so recurrence expands in the
  item's own zone. When the caller omits it, Agenda now defaults it from Identity's `UserPreferences`
  (via the `IUserPreferencesReader` port), falling back to UTC only when the user has no preference.
  The web forms send the saved preference too, and the alert editor defaults its offset from
  `DefaultAlertOffsetMinutes`, in the Agenda's own `agd013_preferences`.
- **Week and day views are a hand-rolled time grid** (`WeekDayGrid`), not a calendar library: an
  hour grid with greedy lane packing for overlapping events, an all-day strip, a now-indicator, and
  click-to-create. The month view keeps antd's `<Calendar>`. All three honour `WeekStartsOn` (the
  week math via a manual `startOfWeek`, the month grid via the dayjs locale's `weekStart`).
- **Agenda settings screen** (`AgendaSettingsPage`, `/agenda/settings`): edits the week start
  (Identity's preference, shown here because only the calendar uses it) and the default alert offset
  (`agd013_preferences`), shows the time zone as inherited from Identity, plus a **default-calendar** picker. Promoting a
  calendar to default demotes the previous one (`UpdateCalendar` clears it first, so the partial
  unique index never sees two).

## Not yet implemented (designed / planned)

| Area | Status | Phase |
|---|---|---|
| **Google Calendar sync** | `agd009`–`agd012` (binding/link/cursor/conflict), `ICalendarSyncProvider` + Google impl, push/echo suppression, conflict log — none built. Depends on [Integrations](../../integrations/en/overview.md) I1 (done). | 5 |
| **Google Tasks sync** | `ITaskSyncProvider` reusing the sync machinery. | 6 |
| **Assistant command catalog** | Commands are commandable (D6), but the descriptor registration (`create_reminder`, `create_task`, `create_event`, `complete_task`, `snooze_reminder`, `whats_my_day`) for Assistant is not wired. | 7 |
| **Beyond** | Note↔event links, NL quick-add, travel time, location alerts, ICS/CalDAV, Microsoft/Apple providers, Finances due dates in the day view. | — |

## Known open points

1. **Calendar UI library vs. hand-rolled grid** — **decided: hand-rolled** (`WeekDayGrid`), no new
   dependency. Revisit only if drag-to-move/resize or multi-day spanning bars are wanted.
2. **Subtask depth** capped at one level (matches Google Tasks fidelity).
3. **Whether Finances migrates to the RRULE engine** — not a prerequisite; revisit if a third
   recurrence consumer appears.

## B11: live Agenda block

Built, uncommitted. `/Agenda` inserts an agenda-list rich block. Source defaults to Today, reading
the existing account-local unified day endpoint (events, tasks and reminders, including their
current statuses). The other mode selects an active task list and shows only Todo/InProgress tasks
in that list. Both modes are read-only: no completion, acknowledgment or event edits inside Notes.
Open in Agenda navigates to the relevant Agenda screen.

Saved configuration is only `{ mode: "today" }` or `{ mode: "tasks", listId }`; empty props default
to today. Live titles, list names, statuses, dates and results never enter the block, autosave,
search text or version snapshots. Changing mode clears the prior list. Unconfigured, deleted,
foreign or archived lists show an explicit unavailable/choose-list state and do not fall back to
loading all tasks. The list id is validated against the authenticated user's current active lists
before mounting the tasks query.

Queries reuse the existing Agenda hooks, services and query-key families. Normal Agenda mutations
refresh active blocks through the same cache; Refresh explicitly invalidates the selected source.
Standard cache staleness/focus behavior still applies; this is not polling or a push subscription.
Reads/refreshes do not update the note. Displayed dates use the account time zone and existing
Agenda formatters; deciding which day is today remains server-side. API failures hide stale rows and
allow retry.

Public view returns Private content before mounting any Agenda query, even with owner data already
cached. Private notes require the Agenda feature and the source's permissions: Today requires
agenda.events.read/tasks.read/reminders.read; list mode requires agenda.tasks.read/lists.read.
Read-mode/locked notes can view and refresh but cannot change source/list. Markdown/HTML exports
emit only [Agenda], without source ids or live values; browser PDF printing uses the currently
rendered owner view. Block ids and configuration round-trip through the editor and versions.

No backend endpoint, schema, migration or cross-module backend dependency change. Browser/live
account validation is pending. The upcoming-events mode and Finance summaries are now built below. Radar/Databases/Projects blocks depend on
those modules.

## B11: upcoming Agenda events

Built, uncommitted. The existing /Agenda block now offers Upcoming events. Configuration is {
mode: "events", days: 7 | 30, calendarId?: string }; missing/unsupported days defaults to 7.
Missing calendarId means all active calendars, independently of the Calendar screen visibility
toggles. A configured calendar must belong to the current active owner list.
Missing/archived/foreign calendars show an explicit unavailable state and never fall back to all
calendars. An empty active list issues no event query.

The window covers today plus the next 6 or 29 calendar dates in the account time zone. Both local
midnights are converted independently to UTC, preserving DST changes. The exclusive end is the
next midnight after the last date. Ongoing and future events are displayed in start order;
ended/cancelled events and occurrences starting at/after the exclusive end are hidden.
All-day/multi-day ranges use the existing Agenda formatter, including the exclusive midnight end.
The window is resolved on rendering/reopening and Refresh; no background polling or midnight timer
is added.

Reads reuse useCalendars/useEventOccurrences and Agenda query keys. The existing events endpoint
expands recurrence rules and applies occurrence edits/cancellations; Notes does not implement
another recurrence engine or request series details. Calendar filters serialize as repeated
calendarIds keys for ASP.NET query binding. Normal event/calendar mutations invalidate active
blocks; Refresh reloads both families. Query failures hide stale results and allow retry.

Requires the Agenda feature and both agenda.events.read and agenda.calendars.read.
Locked/read-mode notes can view and refresh but cannot configure. Changing source clears old
filters. Only days/calendar identity are persisted; names, occurrences and resolved dates never
enter autosave, history or templates. Public pages guard before source queries, even with owner
caches. Markdown/HTML still emit [Agenda]; browser PDF uses the rendered owner view. No backend,
schema or migration change.

Verification: 909 frontend tests passed, including 16 added cases for account-day/DST bounds,
expanded occurrences, ongoing/all-day events, exclusive end, source access/privacy, configuration
and mutation invalidation. Production build/typecheck, Notes and touched Agenda service ESLint,
and Notes antd lint passed. Backend unchanged; backend suites were not rerun. Browser/live-account
validation remains pending. No commit or local database change.
