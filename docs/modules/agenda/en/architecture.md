# Architecture

[← Back to index](../README.md) · Related: [Data Model](data-model.md), [Alerts & Sweep](alerts-and-sweep.md)

---

## 1. Project layout

Layered projects under `backend/src/Modules/Agenda/`:

```
Pottmayer.Pandora.Modules.Agenda.
  Abstractions      → AgendaModule registration, AgendaOptions, AgendaCategories
  Application       → Commands, Queries, Subscribers, Sweep (dispatch commands), Reminders, Tasks,
                      Mapping, Dtos, Errors, DI
  Contracts         → (empty today — Agenda publishes NotifyUserRequested from Channels' contracts)
  Domain            → Aggregates, ValueObjects, Recurrence (the RRULE engine), Ports (repositories)
  Infrastructure    → Jobs: ReminderSweep / TaskAlertSweep / EventAlertSweep background services, DI
  Persistence       → EntityConfigs, Repositories, AgendaDbContext, DI
  Presentation      → Controllers (Reminders, Tasks, TaskLists, Calendars, Events, Alerts, Today), DI
```

Design style: **DDD aggregates** with private constructors + static factories, `TimeProvider`
injected for all time reads, a **command/query** application layer (one folder per use case), and
every user action expressed as a command so [Assistant](../../assistant/en/product-plan.md) can invoke
it directly (D6).

## 2. Domain building blocks

### Aggregates (`Domain/Aggregates`)

| Aggregate root | Responsibility / key invariants |
|---|---|
| **Calendar** (`agd001`) | Named container of events. At most one `default` per user; archive hides, delete of a non-empty calendar is refused. |
| **Event** (`agd002`) | Occupies time; optional RRULE. Occurrences computed on read; `EventOccurrence` is the in-memory materialization; deviations stored as overrides. Soft delete. |
| **EventOccurrenceOverride** (`agd003`) | A per-occurrence deviation keyed by `(event_id, original_starts_at)`: cancelled (EXDATE) or edited fields. |
| **TaskList** (`agd004`) | Named container of tasks. At most one `default` per user; archive hides. |
| **TaskItem** (`agd005`) | Status/priority/due; one level of subtasks (enforced in the aggregate); recurrence materialized one instance at a time — completing closes the row and inserts the next from the RRULE, carrying fields and alerts. |
| **Reminder** (`agd006`) | A ping. Single-shot (rrule NULL) guarded by status; recurring guarded by the dispatch ledger. |
| **ReminderDispatch** (`agd006x`) | Per-occurrence dispatch ledger for recurring reminders; carries per-occurrence ack/snooze. |
| **Alert** (`agd007`) | Polymorphic scheduling primitive over `Task`/`Event`/`Reminder`, keyed by `(subject_type, subject_id)`; signed `offset_minutes`; optional explicit channels. |
| **AlertDispatch** (`agd008`) | Idempotency ledger for alert firing, keyed by `(alert_id, occurrence_starts_at)`; no ack/snooze. |

### Value objects (`Domain/ValueObjects`)

`ReminderStatus`, `TaskItemStatus`, `TaskPriority`, `EventStatus`, `CalendarOrigin` (`Local` \|
`External`), `AlertSubjectType` (`Task` \| `Event` \| `Reminder`).

### Recurrence engine (`Domain/Recurrence`)

`RecurrenceRule` (parses/holds the RFC 5545 subset), `RecurrenceFrequency`, `WeekdayOrdinal`
(`-1FR`-style ordinals), `EventExpander` (expands a series into occurrences within a window),
`EventAlertExpansion` (expands a subject's alerts to anchor instants). Recurrence expands in the
item's own IANA `time_zone`, so "every Monday at 09:00" survives a DST change.

### Ports (`Domain/Ports`)

Repositories, one per aggregate: `ICalendarRepository`, `IEventRepository`,
`IEventOccurrenceOverrideRepository`, `ITaskListRepository`, `ITaskRepository`,
`IReminderRepository`, `IReminderDispatchRepository`, `IAlertRepository`, `IAlertDispatchRepository`.

## 3. The sweeps

Rather than the single `AlertSweepBackgroundService` of the original plan, the implementation runs
**three specialized hosted services**, each draining a mediator command in its own unit of work:

| Service | Command | Guards idempotency with |
|---|---|---|
| `ReminderSweepBackgroundService` | `DispatchDueReminders` | reminder `status` (single-shot) + `agd006x` ledger (recurring) |
| `TaskAlertSweepBackgroundService` | `DispatchDueTaskAlerts` | `agd008` ledger `(alert_id, occurrence)` |
| `EventAlertSweepBackgroundService` | `DispatchDueEventAlerts` | `agd008` ledger; expands the event series to anchors first |

Each tick scans a window `[now − grace, now + lookahead]` (grace default ~15 min covers downtime;
lookahead 0 by default), expands recurring subjects to anchors, and for each due anchor writes a
dispatch row (the idempotency key) and publishes `NotifyUserRequested` to Channels. A crash mid-tick
replays cleanly on the next one. See [Alerts & Sweep](alerts-and-sweep.md).

## 4. Inbound (Telegram buttons)

`InboundInteractionReceivedHandler` + `TaskInteractionHandler` subscribe to Channels'
`InboundInteractionReceived` for `owner_module = agenda`, acting on `task_done` / `snooze_*`. Channels
has already consumed the interaction, so a second tap is "expired".

## 5. Key design decisions & deviations

| # | Decision | Note |
|---|---|---|
| **D1** | One alert primitive, swept in the background. | Implemented as **three** sweeps (reminder, task-alert, event-alert) rather than one, because each subject type expands differently. |
| **D2** | Occurrences computed, not stored — except a **recurring task**, materialized one instance at a time. | An event is `row + rrule` expanded on read; a task is two rows (closed + next) so history survives and Google Tasks fidelity holds. |
| **D3** | Scheduling lives here; Channels only sends now. | A due time is a column; completing/rescheduling before firing is a local update, nothing to cancel downstream. |
| **D4** | Absolute time + per-item IANA zone. | `time_zone` is carried **on the row** (reminder/task/event/calendar) because recurrence must expand in the *item's own* zone. Identity's `UserPreferences` now carries a user-level default; Agenda does not yet consume it as the default for new items. |
| **—** | The reminder dispatch ledger is `agd006x` (reminder-scoped), not the polymorphic `agd008`. | Honest shape until Alert covered reminders; migrates to `agd008` later. |

## 6. Cross-cutting rules

- **Multi-tenant by user.** Every table has `user_id NOT NULL` and an index on it; endpoints scoped to
  the token's user.
- **`TimeProvider` everywhere** — sweeps, TTLs and recurrence anchors are computed against injected time.
- **Delete guards** — deleting a non-empty list/calendar is refused (archive instead); deleting a
  parent task cascades its subtasks.
