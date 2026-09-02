# Tasks

[← Back to index](../README.md) · Related: [Alerts & Sweep](alerts-and-sweep.md), [Data Model](data-model.md)

---

A **task** is something to do, with a workflow: a status, a priority, an optional due date, optional
one-level subtasks, and optional recurrence. Tasks live in **task lists** (`agd004`), the Apple
Reminders *list* / Todoist *project*.

## 1. Lists

A user has at least one list; at most one is the `default` (partial unique index). A list is
**archived** to hide it, not deleted — deleting a list that still holds tasks is refused
(`ON DELETE RESTRICT`); the app archives instead. Lists carry a `position` for ordering.
(`CreateTaskList`, `UpdateTaskList`, `DeleteTaskList`.)

## 2. The task

| Field | Behaviour |
|---|---|
| `status` | `Todo → InProgress → Done`, or `Cancelled`. `completed_at` stamped on Done. |
| `priority` | `None \| Low \| Medium \| High`. |
| `due_at` + `due_has_time` | A task "for tomorrow" is not due at 00:00 — `due_has_time` drives rendering and the default alert offset. |
| `parent_task_id` | Subtasks are tasks, **one level deep** (enforced in the aggregate). Deleting a parent cascades its subtasks. |
| `position` | Ordering within the list. |
| `rrule` | Recurrence, top-level tasks only. |

## 3. Complete / reopen, and recurrence

- **Complete** (`CompleteTask`) sets `status = Done` + `completed_at`.
- **Reopen** (`ReopenTask`) returns it to `Todo`.
- **Recurring task.** A recurring task is materialized **one instance at a time**, not as a stored
  series. Completing the current row closes it (`Done`, `completed_at`) and the application inserts the
  **next instance** from the RRULE, carrying its fields and its alerts. Two rows, not one mutable row,
  so history survives. This is why "a recurring weekly task completed today reappears next week with
  its alerts."

## 4. Alerts & overdue

Alerts attach to a task through the polymorphic `Alert` (`subject_type = Task`) — one of the two wired
subject types (`Event` is the other; `Reminder` keeps its own `agd006x` ledger instead). `offset_minutes`
is signed relative to `due_at` (`0` at the instant, `-15` fifteen minutes before); `channels` NULL
resolves from the user's Channels preference. `TaskAlertSweepBackgroundService` dispatches them, and the
Telegram *Done* button completes the task. See [Alerts & Sweep](alerts-and-sweep.md).

## 5. Commands & endpoints

Lists: `CreateTaskList`, `UpdateTaskList`, `DeleteTaskList`. Tasks: `CreateTask`, `UpdateTask`,
`CompleteTask`, `ReopenTask`, `DeleteTask`. Alerts: `CreateAlert`, `DeleteAlert`. HTTP: see
[API Reference](api-reference.md) — `/agenda/task-lists`, `/agenda/tasks` (with
`{id}/complete`, `{id}/reopen`), and `/agenda/{subjectType}/{id}/alerts`.
