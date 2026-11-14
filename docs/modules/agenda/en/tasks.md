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

## 6. Google Tasks

The user picks a Google account and some of its task lists in the Agenda settings; each becomes its own
Pandora list, named like it. A **binding** (`agd012`) links the two and keeps the cursor; a **link**
(`agd014`) links each Pandora task to its Google task. It is always both ways: a Google task list belongs
to its account. An account connected before phase 6 lacks the `auth/tasks` scope and has to reconnect.

**Cycle.** The calendars' job (`GoogleCalendarSyncBackgroundService`, every 2 minutes) also runs one
`GoogleTasksSync` cycle per binding in `ok`: Pandora's edits first, then Google's. Google Tasks has no
sync token. The first cycle, every 30 days and after a conflict Google won list every live task and
remove what Google no longer has; the others ask for what changed since the last listing started
(`updatedMin`, one minute early), deletions included.

**What each side keeps** (`GoogleTaskMapper`, a pure function):

| Pandora | Google |
|---|---|
| title, notes | `title` (`(sem título)` when empty), `notes` |
| due date | `due`, a date only. Pandora sends the date in the task's zone. A date from Google keeps Pandora's time of day when it is the same date; a new date becomes a whole day |
| `Done`, `Cancelled` / `Todo`, `InProgress` | `completed` / `needsAction` |
| subtask | `parent` (one level, like Pandora) |
| priority, alerts, recurrence | nothing: they stay in Pandora, and reading never clears them |

A recurring task goes to Google as a plain task. Completing it, here or in Google, spawns the next
instance in Pandora, which goes to Google on the next cycle.

**Writing** (`GoogleTasksPush`). Creating, editing, completing, reopening or deleting a task in a bound
list marks its link pending (`pending_push_at`) in the same transaction. Google picks a task's id, so a
new task's link has none until its insert, and a subtask waits for its parent. Google Tasks takes no
`If-Match`: an update reads the task first, and an etag other than the link's means it changed there
too. The newer edit wins (Google's `updated` against the time of Pandora's edit) and the other goes to
`agd011` and the conflict list, as with events. A task deleted in Google while Pandora edited it stays
deleted. An insert whose answer is lost is retried and can leave a duplicate in Google.

**What stays fixed.** A bound list cannot be deleted (`Agenda.TaskListBound`); unlink it first.
Unlinking removes the binding and the links; the list and its tasks stay, and nothing changes in Google.
A revoked account leaves its bindings in `needs-reconnect`; a disconnected one unlinks them.
