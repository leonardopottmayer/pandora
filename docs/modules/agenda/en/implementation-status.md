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
| **Alerts** | `agd007` polymorphic + `agd008` dispatch ledger; `Task` subject wired; `TaskAlertSweepBackgroundService`. |
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
- **`Alert.subject_type` admits `Task`/`Event`/`Reminder` but only `Task` is wired**; events use the
  event-alert sweep directly, reminders keep `agd006x`.
- **`time_zone` is carried per row** (reminder/task/event/calendar) so recurrence expands in the
  item's own zone. When the caller omits it, Agenda now defaults it from Identity's `UserPreferences`
  (via the `IUserPreferencesReader` port), falling back to UTC only when the user has no preference.
  The web forms send the saved preference too, and the alert editor defaults its offset from
  `DefaultAlertOffsetMinutes`.
- **Frontend week/day views and an Agenda settings screen** are partially deferred.

## Not yet implemented (designed / planned)

| Area | Status | Phase |
|---|---|---|
| **Google Calendar sync** | `agd009`–`agd012` (binding/link/cursor/conflict), `ICalendarSyncProvider` + Google impl, push/echo suppression, conflict log — none built. Depends on [Integrations](../../integrations/en/overview.md) I1 (done). | 5 |
| **Google Tasks sync** | `ITaskSyncProvider` reusing the sync machinery. | 6 |
| **Assistant command catalog** | Commands are commandable (D6), but the descriptor registration (`create_reminder`, `create_task`, `create_event`, `complete_task`, `snooze_reminder`, `whats_my_day`) for Assistant is not wired. | 7 |
| **Honour `WeekStartsOn` in the calendar grid** | `TimeZone` and `DefaultAlertOffsetMinutes` are now consumed (see above). `WeekStartsOn` is still not honoured: antd's `<Calendar>` derives the first day of the week from the dayjs locale, so wiring it means configuring the dayjs locale's `weekStart` globally — folded into the week/day view work. | follow-up (phase 2) |
| **Beyond** | Note↔event links, NL quick-add, travel time, location alerts, ICS/CalDAV, Microsoft/Apple providers, Finances due dates in the day view. | — |

## Known open points

1. **Calendar UI library vs. hand-rolled grid** — affects only the week/day polish.
2. **Subtask depth** capped at one level (matches Google Tasks fidelity).
3. **Whether Finances migrates to the RRULE engine** — not a prerequisite; revisit if a third
   recurrence consumer appears.
