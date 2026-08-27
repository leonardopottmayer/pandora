# Data Model

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Alerts & Sweep](alerts-and-sweep.md)

PostgreSQL schema **`agenda`**. Conventions: PK `uuid DEFAULT uuid_generate_v7()`, `TIMESTAMPTZ`
everywhere (time stored absolute, D4), audit columns `created_by/created_at/updated_by/updated_at`,
named constraints, enums as `VARCHAR` + `CHECK` stored **PascalCase** (to match
`agd006_reminder.status`). Each item carries its own IANA `time_zone`, because recurrence expands in
the item's own zone. (Identity's `UserPreferences` carries a user-level default zone, which Agenda does not yet consume as the per-item default.)

Migrations live in `migrations/migrations/agenda/`.

## Table catalog

| # | Table | Contents |
|---|---|---|
| agd001 | `calendar` | Named container of events |
| agd002 | `event` | Calendar event (+ RRULE, expanded on read) |
| agd003 | `event_occurrence_override` | Per-occurrence deviation from an event series |
| agd004 | `task_list` | Named container of tasks |
| agd005 | `task` | A task (status/priority/due/subtasks/recurrence) |
| agd006 | `reminder` | A ping at an instant (single-shot or recurring) |
| agd006x | `reminder_dispatch` | Per-occurrence dispatch ledger for recurring reminders |
| agd007 | `alert` | Polymorphic "notify me about *subject*" primitive |
| agd008 | `alert_dispatch` | Idempotency ledger for alert firing |
| agd009–agd012 | *(reserved)* | Calendar binding / sync link / cursor / conflict — **not yet implemented** (phases 5–6) |

---

## agd001_calendar

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | |
| `name` | varchar(200) NOT NULL | |
| `color` | varchar(50) NULL | |
| `is_default` / `is_visible` | bool | at most one default per user (`uq_agd001_user_default`, partial `WHERE is_default`) |
| `time_zone` | varchar(100) NOT NULL DEFAULT 'UTC' | recurrence expands here |
| `origin` | varchar(20) NOT NULL DEFAULT 'Local' | `Local \| External` (`chk_agd001_origin`); only `Local` matters until Google sync |
| `archived_at` | timestamptz NULL | archive hides; deleting a calendar with live events is refused by the app |

## agd002_event

An event is **calculated, never stored**: one row plus an rrule, expanded into occurrences on read.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id`, `calendar_id` | uuid | `fk_agd002_calendar … ON DELETE RESTRICT` (a calendar keeps its events) |
| `title` / `description` / `location` / `url` | | `url` = meeting link |
| `starts_at` / `ends_at` | timestamptz NOT NULL | all-day ⇒ midnight in `time_zone`, end exclusive |
| `is_all_day` | bool | |
| `time_zone` | varchar(100) | IANA, per event |
| `rrule` | text NULL | RFC 5545 subset, verbatim; NULL ⇒ single occurrence |
| `recurrence_ends_at` | timestamptz NULL | denormalized last-occurrence bound (from UNTIL/COUNT) so a range query prunes finished series by index |
| `status` | varchar(20) DEFAULT 'Confirmed' | `Confirmed \| Tentative \| Cancelled` |
| `deleted_at` | timestamptz NULL | soft delete (a future inbound sync can resurrect) |

Indexes: `ix_agd002_user_id`, `ix_agd002_calendar_id` (range query + delete guard).

## agd003_event_occurrence_override

Natural key `(event_id, original_starts_at)` — which occurrence, by its on-grid start.

| Column | Type | Notes |
|---|---|---|
| `event_id` | uuid → agd002 | `ON DELETE CASCADE` |
| `original_starts_at` | timestamptz NOT NULL | identifies the occurrence |
| `is_cancelled` | bool | the EXDATE case (occurrence disappears) |
| `starts_at` / `ends_at` / `title` / `description` / `location` | NULL | non-null columns override the series for that one occurrence; NULL falls back |

Constraint: `uq_agd003_event_occurrence (event_id, original_starts_at)`. Editing "this and future"
instead **splits** the series (a new `agd002` row) and writes no override.

## agd004_task_list

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | |
| `name` | varchar(200) | |
| `is_default` | bool | `uq_agd004_user_default` (partial `WHERE is_default`) |
| `position` | int | ordering |
| `archived_at` | timestamptz NULL | |

## agd005_task

A recurring task is materialized **one instance at a time**: completing closes the current row and the
app inserts the next from the RRULE, carrying fields and alerts (two rows, so history survives).

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id`, `list_id` | uuid | `fk_agd005_list … ON DELETE RESTRICT` |
| `parent_task_id` | uuid NULL → agd005 | subtasks; one level, enforced in the aggregate; `ON DELETE CASCADE` |
| `title` / `notes` | | |
| `due_at` / `due_has_time` | timestamptz / bool | a task "for tomorrow" is not due at 00:00 — `due_has_time` drives rendering + default alert offset |
| `priority` | varchar(10) DEFAULT 'None' | `None \| Low \| Medium \| High` |
| `status` | varchar(20) DEFAULT 'Todo' | `Todo \| InProgress \| Done \| Cancelled` |
| `completed_at` | timestamptz NULL | |
| `time_zone` | varchar(100) | recurrence expands here |
| `rrule` | text NULL | top-level tasks only; NULL ⇒ not recurring |
| `position` | int | |
| `deleted_at` | timestamptz NULL | soft delete |

Indexes: `ix_agd005_user_id`, `ix_agd005_list_status (list_id, status)` (list screen),
`ix_agd005_parent_task_id` (partial).

## agd006_reminder

A ping at an instant. Single-shot (`rrule` NULL) is guarded by `status`; recurring is guarded by the
`agd006x` ledger, and its `status` stays `Scheduled` for the life of the series.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | |
| `title` / `notes` | | |
| `remind_at` | timestamptz NOT NULL | |
| `time_zone` | varchar(100) DEFAULT 'UTC' | |
| `rrule` | text NULL | RFC 5545 subset, verbatim; NULL ⇒ single-shot |
| `recurrence_ends_at` | timestamptz NULL | denormalized series end so the sweep prunes finished series by index |
| `status` | varchar(20) | `Scheduled \| Notified \| Acknowledged \| Snoozed \| Cancelled` |
| `snoozed_until` / `acknowledged_at` | timestamptz NULL | single-shot action |

Indexes: `ix_agd006_user_id`, `ix_agd006_status_remind_at` (single-shot sweep hot path),
`ix_agd006_recurrence_ends_at` (partial `WHERE rrule IS NOT NULL`, recurring sweep).

## agd006x_reminder_dispatch

Per-occurrence dispatch ledger for recurring reminders — what makes the sweep idempotent when the
`status` column cannot (a recurring reminder fires many times).

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `reminder_id` | uuid → agd006 | `ON DELETE CASCADE` |
| `user_id` | uuid | |
| `occurrence_starts_at` | timestamptz NOT NULL | |
| `dispatched_at` / `correlation_id` | | |
| `is_late` | bool | fired from the grace window (a suspended machine caught up); informational |
| `acknowledged_at` / `snoozed_until` | timestamptz NULL | per-occurrence action (ack/snooze act on the occurrence, never the series) |

Constraint: `uq_agd006x_reminder_occurrence (reminder_id, occurrence_starts_at)` — one dispatch per
(reminder, occurrence). Index `ix_agd006x_snoozed_until` (partial) for the snooze re-fire path.

> **Naming note.** This ledger is `agd006x_` (an extension of the reminder aggregate), not the
> polymorphic `agd008` — the honest shape until Alert covers reminders; it migrates to `agd008` later.

## agd007_alert

The polymorphic scheduling primitive: one row per wanted ping, keyed to a subject by
`(subject_type, subject_id)` **with no FK** (validated in the app, removed with the subject).

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | |
| `subject_type` | varchar(20) | `Task \| Event \| Reminder` (`chk_agd007_subject_type`) — **only `Task` is wired today** |
| `subject_id` | uuid NOT NULL | |
| `offset_minutes` | int NOT NULL | signed, relative to the subject anchor (`0` = at the instant, `-15` = 15 min before) |
| `channels` | text[] NULL | NULL ⇒ resolve from the user's Channels preference; else explicit (`email`, `telegram`) |
| `is_enabled` | bool DEFAULT true | |

Indexes: `ix_agd007_user_id`, `ix_agd007_subject (subject_type, subject_id)`,
`ix_agd007_enabled_subject_type` (partial `WHERE is_enabled`, the sweep scan root).

## agd008_alert_dispatch

The alert dispatch ledger. One row the first time an alert fires for a subject anchor.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `alert_id` | uuid → agd007 | `ON DELETE CASCADE` |
| `user_id` | uuid | |
| `occurrence_starts_at` | timestamptz NOT NULL | |
| `dispatched_at` / `correlation_id` | | |
| `is_late` | bool | fired from the grace window; informational |

Constraint: `uq_agd008_alert_occurrence (alert_id, occurrence_starts_at)`. No ack/snooze — a task
alert's button completes the task itself.

## agd009–agd012 *(reserved — not implemented)*

Google sync (phases 5–6): `calendar_binding`, `sync_link` (`remote_id`, `etag`, hashes),
`sync_cursor`, `sync_conflict`. See [product-plan.md](product-plan.md).
