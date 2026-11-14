# Implementation Status

[← Back to index](../README.md)

A snapshot of what is built versus what is designed but not yet implemented. The forward roadmap lives
in [product-plan.md](product-plan.md).

---

## Implemented (phases 1–6, frontend, Assistant tools)

| Area | Notes |
|---|---|
| **Module scaffold** | Seven layered projects; `agenda` schema; DI + module registration. |
| **Reminders** | `agd006` single-shot + recurring; `agd006x` per-occurrence ledger; acknowledge/snooze (series and per-occurrence); rename; `ReminderSweepBackgroundService`. |
| **Recurrence engine** | `RecurrenceRule` parse + `EventExpander` expand, RFC 5545 subset, DST-aware, `-1FR`-style ordinals; expands in the item's `time_zone`. |
| **Tasks** | `agd004` lists + `agd005` tasks; one-level subtasks; priority; due with/without time; complete/reopen; recurring materialized one instance at a time carrying alerts. |
| **Alerts** | `agd007` polymorphic + `agd008` dispatch ledger; `Task` and `Event` subjects wired; `TaskAlertSweepBackgroundService` + `EventAlertSweepBackgroundService`. |
| **Calendar & events** | `agd001` calendars, `agd002` events (computed occurrences), `agd003` overrides; this / this-and-future / all edit scopes. |
| **Preferences** | `agd013_preferences`: the default alert offset. The time zone and week start come from Identity. |
| **Google Calendar (5)** | `agd009` bindings (`read` / `read-write` by the account's access) + `agd010` links + `agd011` conflicts; `GoogleCalendarSync` (full listing a year back, then `syncToken` changes, 410 ⇒ full again, monthly full); `GoogleEventMapper` (timed, all-day, RRULE + EXDATE, exceptions as overrides, reminders as alerts); series Pandora cannot expand brought as instances; `GoogleCalendarPush` (pending links inserted, patched with `If-Match` or deleted, occurrences as instance patches, alerts as popup reminders, etag echo suppression, newer edit wins with the loser logged); read-only guard for `read` bindings; `GoogleCalendarSyncBackgroundService` every 2 minutes; revoked ⇒ `needs-reconnect`, disconnected ⇒ unlinked; `AgendaAccountUsageSource`; the settings section with direction and conflicts. See [Calendar & Events §6](calendar-and-events.md#6-google-calendar). |
| **Google Tasks (6)** | `agd012` bindings + `agd014` links, conflicts in `agd011` (`subject_type`); `GoogleTasksSync` (full listing, then `updatedMin` changes with deletions, monthly full); `GoogleTaskMapper` (due date only, Pandora's time kept on the same date, completed/needsAction, one-level subtasks; priority, alerts and recurrence stay in Pandora); `GoogleTasksPush` (pending links inserted, updated after reading the task, deleted; newer edit wins with the loser logged); recurring tasks spawn their next instance on completion from either side; the same 2-minute job; revoked ⇒ `needs-reconnect`, disconnected ⇒ unlinked; the settings section. See [Tasks §6](tasks.md#6-google-tasks). |
| **Today** | `GET /agenda/today` unified read (events + tasks + reminders). |
| **Inbound buttons** | `InboundInteractionReceivedHandler` + `TaskInteractionHandler` for `task_done` / `snooze_*` from Channels. |
| **Assistant tools** | 15 `IAssistantTool`s in `Application/Assistant`: `create_reminder`, `list_reminders`, `rename_reminder`, `reschedule_reminder`, `cancel_reminder`, `create_task`, `list_tasks`, `update_task`, `complete_task`, `reopen_task`, `delete_task`, `create_event`, `update_event`, `delete_event`, `list_agenda`. The Assistant resolves relative dates; Agenda parses nothing. |
| **API** | Reminders, task-lists, tasks, calendars, events, alerts, preferences, today and Google controllers. See [API Reference](api-reference.md). |
| **Frontend** | `client-web/src/modules/agenda`: Today, Reminders, Tasks, Calendar (month, week and day) and Settings (`/agenda/settings`). Forms are 640 px modals that stack on phones; screens tell a failed request from an empty result. |

Notes shows Agenda data in its live Agenda block, which reads Agenda's own endpoints; see the Notes
docs.

### Decisions that differ from the first design

- **Three sweeps, not one.** `ReminderSweep`, `TaskAlertSweep`, `EventAlertSweep` replace the single
  `AlertSweepBackgroundService` — each subject type expands differently.
- **Reminder dispatch ledger is `agd006x`** (reminder-scoped), not the polymorphic `agd008`.
  `Alert.subject_type` admits `Task`/`Event`/`Reminder`, but only `Task` and `Event` are wired.
- **`time_zone` is carried per row** (reminder/task/event/calendar) so recurrence expands in the
  item's own zone. When the caller omits it, Agenda defaults it from Identity's `UserPreferences`
  (via the `IUserPreferencesReader` port), falling back to UTC only when the user has no preference.
- **Week and day views are a hand-rolled time grid** (`WeekDayGrid`), not a calendar library: an
  hour grid with greedy lane packing for overlapping events, an all-day strip, a now-indicator, and
  click-to-create. The month view keeps antd's `<Calendar>`. All three honour `WeekStartsOn` (the
  week math via a manual `startOfWeek`, the month grid via the dayjs locale's `weekStart`). Revisit
  only if drag-to-move/resize or multi-day spanning bars are wanted.
- **Agenda settings screen** edits the week start (Identity's preference, shown here because only the
  calendar uses it) and the default alert offset, shows the time zone as inherited from Identity, and
  picks the **default calendar**. Promoting a calendar to default demotes the previous one
  (`UpdateCalendar` clears it first, so the partial unique index never sees two).

## Not yet implemented (designed / planned)

| Area | Status | Phase |
|---|---|---|
| **Beyond** | Note↔event links, NL quick-add, travel time, location alerts, ICS/CalDAV, Microsoft/Apple providers, Finances due dates in the day view. | — |

## Known open points

1. **Subtask depth** capped at one level (matches Google Tasks fidelity).
2. **Whether Finances migrates to the RRULE engine** — not a prerequisite; revisit if a third
   recurrence consumer appears.
