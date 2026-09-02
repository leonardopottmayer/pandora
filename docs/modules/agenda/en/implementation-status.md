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
  `DefaultAlertOffsetMinutes`.
- **Week and day views are a hand-rolled time grid** (`WeekDayGrid`), not a calendar library: an
  hour grid with greedy lane packing for overlapping events, an all-day strip, a now-indicator, and
  click-to-create. The month view keeps antd's `<Calendar>`. All three honour `WeekStartsOn` (the
  week math via a manual `startOfWeek`, the month grid via the dayjs locale's `weekStart`).
- **Agenda settings screen** (`AgendaSettingsPage`, `/agenda/settings`): surfaces the scheduling
  defaults (time zone, week start, default alert offset) via the shared preferences context — one
  source of truth with Identity, not a copy — plus a **default-calendar** picker. Promoting a
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
