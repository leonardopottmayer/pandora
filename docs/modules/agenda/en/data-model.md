# Data Model

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Alerts & Sweep](alerts-and-sweep.md)

PostgreSQL schema **`agenda`**. Conventions: PK `uuid DEFAULT uuid_generate_v7()`, `TIMESTAMPTZ`
everywhere (time stored absolute, D4), audit columns `created_by/created_at/updated_by/updated_at`,
named constraints, enums as `VARCHAR` + `CHECK` stored **PascalCase** (to match
`agd006_reminder.status`). Each item carries its own IANA `time_zone`, because recurrence expands in
the item's own zone. (When a create request omits it, Agenda defaults it from Identity's `UserPreferences` — a user-level default zone — falling back to UTC only when the user has no preference.)

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
| agd009 | `calendar_binding` | A Pandora calendar fed by a Google calendar |
| agd010 | `event_link` | A Pandora event linked to its Google event |
| agd011 | `sync_conflict` | Events and tasks both sides changed; the version that lost |
| agd012 | `task_list_binding` | A Pandora task list synced with a Google Tasks list |
| agd014 | `task_link` | A Pandora task linked to its Google task |
| agd013 | `preferences` | What a user prefers about the Agenda itself |

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
| `subject_type` | varchar(20) | `Task \| Event \| Reminder` (`chk_agd007_subject_type`) — **`Task` and `Event` are wired**; `Reminder` keeps the `agd006x` ledger instead |
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

## agd013_preferences

What a user prefers about the Agenda itself, one row per user. No row means the defaults.

| Column | Type | Notes |
|---|---|---|
| `user_id` | uuid PK | |
| `default_alert_offset_minutes` | int NOT NULL DEFAULT -15 | signed, relative to the item; what the alert editor starts from |
| `created_by`, `created_at`, `updated_by`, `updated_at` | | audit |

## agd009_calendar_binding

One Pandora calendar fed by one Google calendar of one Integrations account. See
[Calendar & Events §6](calendar-and-events.md#6-google-calendar).

| Column | Type | Notes |
|---|---|---|
| `id`, `user_id` | uuid | |
| `calendar_id` | uuid → agd001 | unique (`uq_agd009_calendar`) |
| `external_account_id` | uuid | the Integrations account; no FK (another module) |
| `remote_calendar_id` | varchar(255) | unique per account (`uq_agd009_account_remote`) |
| `direction` | varchar(20) DEFAULT 'read' | `read \| read-write`; `read-write` when the account owns or may edit the Google calendar |
| `sync_token` | text NULL | Google's cursor; NULL ⇒ the next cycle lists everything |
| `full_synced_at` / `last_synced_at` | timestamptz NULL | a full listing runs again after 30 days |
| `status` | varchar(20) DEFAULT 'ok' | `ok \| needs-reconnect \| error` |
| `failure_count` / `last_error` | int / varchar(1000) | five failures in a row ⇒ `error` |

## agd010_event_link

One Pandora event ↔ one Google event of a binding: what keeps a sync from duplicating.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `binding_id` | uuid → agd009 | |
| `event_id` | uuid → agd002 | unique (`uq_agd010_event`) |
| `remote_event_id` | varchar(1024) | unique per binding (`uq_agd010_binding_remote`) |
| `series_remote_id` | varchar(1024) NULL | set on the single events made from the instances of a series Pandora cannot expand |
| `etag` / `remote_updated_at` | | what Google said last |
| `reminders_hash` | varchar(64) NULL | the Google reminders the event's alerts were written from |
| `pending_push_at` | timestamptz NULL | when Pandora last changed the event without Google having it; partial index `ix_agd010_binding_pending` |
| `created_at` | timestamptz | |

`etag` NULL on a pending link ⇒ the event was born in Pandora and is not in Google yet; its
`remote_event_id` is the id Pandora will insert it with.

## agd011_sync_conflict

An event or task both sides changed between two syncs: the newer edit won, the other is kept here. A log, with
no foreign keys, so it outlives an unlinked calendar.

| Column | Type | Notes |
|---|---|---|
| `id`, `user_id` | uuid | index `ix_agd011_user_detected (user_id, detected_at DESC)` |
| `binding_id` | uuid | an `agd009` or `agd012` binding |
| `subject_type`, `subject_id` | varchar(20), uuid | `event` (`agd002`) or `task` (`agd005`) |
| `title` | varchar(200) | the event's or task's title in Pandora when detected |
| `losing_side` | varchar(20) | `pandora \| google` |
| `losing_version` | jsonb | the version that lost, in Google's event or task shape |
| `detected_at` | timestamptz | |

## agd012_task_list_binding

One Pandora task list synced with one Google Tasks list of one Integrations account, always both ways.
See [Tasks §6](tasks.md#6-google-tasks).

| Column | Type | Notes |
|---|---|---|
| `id`, `user_id`, `external_account_id` | uuid | index `ix_agd012_user_id` |
| `list_id` | uuid → agd004 | unique (`uq_agd012_list`) |
| `remote_list_id` | varchar(255) | unique per account (`uq_agd012_account_remote`) |
| `updated_cursor` | timestamptz NULL | when the last listing started; the next asks for what changed since. NULL ⇒ the next cycle lists everything |
| `full_synced_at`, `last_synced_at` | timestamptz NULL | |
| `status`, `failure_count`, `last_error` | | as in `agd009` |

## agd014_task_link

One Pandora task ↔ one Google task of a binding.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid | |
| `binding_id` | uuid → agd012 | |
| `task_id` | uuid → agd005 | unique (`uq_agd014_task`) |
| `remote_task_id` | varchar(1024) NULL | unique per binding (`uq_agd014_binding_remote`); NULL until a task born in Pandora is inserted (Google picks the id) |
| `etag` / `remote_updated_at` | | what Google said last |
| `pending_push_at` | timestamptz NULL | when Pandora last changed the task without Google having it; partial index `ix_agd014_binding_pending` |
