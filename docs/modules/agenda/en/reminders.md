# Reminders

[← Back to index](../README.md) · Related: [Alerts & Sweep](alerts-and-sweep.md), [Data Model](data-model.md)

---

A **reminder** is a ping at a moment in time, with no workflow — Apple Reminders semantics. It fires,
and the user acknowledges, snoozes, or cancels it. Reminders are standalone (`agd006`), not attached
to a calendar or list.

## 1. Single-shot vs. recurring

The `rrule` column decides the shape, and each shape has a **different idempotency guard**:

| | Single-shot (`rrule` NULL) | Recurring (`rrule` set) |
|---|---|---|
| Fires | once, at `remind_at` | once per occurrence |
| Idempotency guard | the `status` column | the `agd006x_reminder_dispatch` ledger |
| After firing | `status = Notified`; the row is no longer selected, so a restart never re-fires | `status` stays `Scheduled` for the life of the series; one ledger row per occurrence |
| Series end | — | `recurrence_ends_at` (denormalized from UNTIL/COUNT) lets the sweep prune finished series by index |

Recurrence expands in the reminder's own `time_zone`, so "every weekday at 08:00" fires exactly once
per weekday across a DST change.

## 2. Acknowledge & snooze

- **Single-shot.** Acknowledge sets `status = Acknowledged` + `acknowledged_at`; snooze sets
  `status = Snoozed` + `snoozed_until`, which the sweep then treats as the effective `remind_at`.
  (`AcknowledgeReminder`, `SnoozeReminder`, `CancelReminder`.)
- **Recurring.** Ack and snooze act on the **occurrence, never the series** — they are written on the
  `agd006x` ledger row (`acknowledged_at` / `snoozed_until`). A snoozed occurrence re-fires once when
  `snoozed_until` passes (the sweep clears it on re-fire); an acknowledged one never re-fires. The
  series keeps running. (`AcknowledgeOccurrence`, `SnoozeOccurrence`.)

## 3. Delivery

A due reminder is dispatched by `ReminderSweepBackgroundService` (`DispatchDueReminders`), which
publishes `NotifyUserRequested` to [Channels](../../channels/en/overview.md) with the reminder's title
and inline buttons. The Telegram *Snooze 1h* button comes back through
`InboundInteractionReceived` and moves the reminder. See [Alerts & Sweep](alerts-and-sweep.md) for the
sweep window, grace and late-firing.

## 4. Commands & endpoints

`CreateReminder`, `AcknowledgeReminder`, `SnoozeReminder`, `CancelReminder`, `AcknowledgeOccurrence`,
`SnoozeOccurrence`. HTTP: see [API Reference](api-reference.md) — `POST /agenda/reminders`,
`GET /agenda/reminders`, `POST /agenda/reminders/{id}/acknowledge`,
`POST /agenda/reminders/{id}/snooze`, `DELETE /agenda/reminders/{id}`.
