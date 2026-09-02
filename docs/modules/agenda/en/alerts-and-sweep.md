# Alerts & Sweep

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Data Model](data-model.md)

---

## 1. The alert — one scheduling primitive (D1)

Events, tasks and reminders do not each grow notification logic. Every "tell me about this at time T"
is an **`Alert`** row (`agd007`), polymorphic over its subject by `(subject_type, subject_id)` with no
foreign key — validated in the application and removed with the subject.

| Field | Meaning |
|---|---|
| `subject_type` | `Task` \| `Event` \| `Reminder` — **`Task` and `Event` are wired**; reminders keep the `agd006x` ledger instead. |
| `offset_minutes` | Signed, relative to the subject's anchor (a task's `due_at`, an event's occurrence start). `0` = at the instant, `-15` = fifteen minutes before. |
| `channels` | NULL ⇒ resolve from the user's Channels preference for the category; else explicit (`email`, `telegram`). |
| `is_enabled` | The sweep only scans enabled alerts. |

## 2. The three sweeps

Instead of one unified service, three specialized hosted services run, each draining a mediator
command in **one unit of work per subject**:

| Service | Command | Subject | Idempotency ledger |
|---|---|---|---|
| `ReminderSweepBackgroundService` | `DispatchDueReminders` | reminders | `status` (single-shot) / `agd006x` (recurring) |
| `TaskAlertSweepBackgroundService` | `DispatchDueTaskAlerts` | task alerts | `agd008 (alert_id, occurrence)` |
| `EventAlertSweepBackgroundService` | `DispatchDueEventAlerts` | event alerts | `agd008`; expands the event series to anchors first |

## 3. The sweep loop

Every tick, per subject:

```
window = [now - grace, now + lookahead]     # grace covers downtime; lookahead 0 by default
anchors = expand(subject, window)           # 1 for non-recurring, N for recurring (in the item's zone)
for anchor in anchors:
    fire_at = anchor + offset
    if fire_at not in window: continue
    if dispatch row exists for (subject/alert, occurrence): continue   # idempotency
    write dispatch row                       # the idempotency key
    publish NotifyUserRequested              # to Channels
```

- **Idempotency.** The dispatch row's `UNIQUE (…, occurrence_starts_at)` means re-running the sweep
  over the same tick — or restarting mid-tick — never double-fires and never skips. Everything happens
  in one unit of work per alert, so a crash mid-tick replays cleanly on the next.
- **Grace** (default ~15 min) means a laptop that was asleep still delivers the reminder it missed;
  such a firing is flagged `is_late = true` (informational).
- **Look-ahead** is 0 by default — alerts fire on their tick, not early.

## 4. Delivery & buttons

The sweep publishes **`NotifyUserRequested`** (a Channels contract) with the rendered content and
declared inline buttons — because *whoever owns the buttons owns the `NotifyUserRequested`*
(Channels principle). Telegram carries the buttons (*Done*, *Snooze 1h*); email does not.

The tap comes back as Channels' **`InboundInteractionReceived`** with `owner_module = agenda`, handled
by `InboundInteractionReceivedHandler` → `TaskInteractionHandler`:

- `task_done` completes the task (a task alert has no per-occurrence snooze).
- `snooze_*` moves the reminder occurrence.

Channels has already consumed the interaction (single use), so a second tap is "expired", not a second
command.

## 5. Why scheduling lives here (D3)

A due time is a **column on a row**. Rescheduling or completing an item *before* it fires is a local
update with nothing to cancel downstream — exactly what a reminder needs, being the thing whose time
changes. Channels only knows how to *send now*; Agenda decides *when*.
