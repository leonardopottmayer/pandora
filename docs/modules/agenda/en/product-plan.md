# Agenda Module — Roadmap (remaining work)

> **Status:** phases **1–4** (reminders, recurrence, tasks, calendar/events), the frontend and the
> Assistant tools are implemented, and Google → Pandora sync (5a). This file tracks only what is
> **not yet built**: Pandora → Google sync and Google Tasks. For
> what exists, see the module docs: [README](../README.md) · [Overview](overview.md) ·
> [Architecture](architecture.md) · [Data Model](data-model.md) · [Reminders](reminders.md) ·
> [Tasks](tasks.md) · [Calendar & Events](calendar-and-events.md) · [Alerts & Sweep](alerts-and-sweep.md) ·
> [Implementation Status](implementation-status.md).
> 🇧🇷 [Versão em português](../pt-BR/product-plan.md)
>
> Related plans: [Channels](../../channels/en/product-plan.md) ·
> [Integrations](../../integrations/en/product-plan.md) · [Assistant](../../assistant/en/product-plan.md) ·
> [Messaging](../../../architecture/en/messaging.md)

---

## Phase 5 — Google Calendar sync *(5a built 2026-11-09; 5b next)*

Two-way sync between Pandora calendars and Google calendars, in two steps. **5a** (Google → Pandora,
read only) is built: see [Calendar & Events §6](calendar-and-events.md#6-google-calendar-google--pandora).
**5b** sends Pandora's edits to Google.

### Decisions

| # | Decision | Chosen | Over |
|---|---|---|---|
| G1 | Direction | Two-way, in two deliveries: read first, write second | Both at once; read only; write only |
| G2 | Which calendars | The user picks Google calendars; each becomes its own Pandora calendar (1:1), with Google's name and color, tied to that account | Every calendar of the account; merging into an existing Pandora calendar |
| G3 | Alerts on Google events | Google's reminders on the event become Pandora alerts | No alerts by default; Pandora's default offset on every event |
| G4 | Conflicts (5b) | The newest edit wins, by modification time; the losing version is logged in a conflict list | Google always wins; ask on every conflict |
| G5 | Where the engine lives | Inside Agenda: a Google client behind `IGoogleCalendarClient`, a sync job and a local copy, the shape of Communications' Gmail sync | A generic sync module; reading Google live on every screen |
| G6 | Freshness | Polling every 2 minutes with Google's `syncToken`, plus "Sync now" | Google push notifications (need public HTTPS, which the homelab lacks) |

### 5b — Pandora → Google

- **Data:** `agd009.direction` becomes `read-write` (the column exists, always `read` today);
  `agd010` gains `local_hash` and `pending_push_at`; new `agd011_sync_conflict` (`event_id`,
  `binding_id`, which side lost, the losing version as jsonb, `detected_at`).
- **The read-only guard** (`ExternalCalendars`) lets a `read-write` binding's events through.
- **Local writes** in a bound calendar set `pending_push_at` on the link in the same transaction. The
  push runs right after the commit; whatever fails is retried by the next cycle. Creating an event
  inserts it in Google; deleting deletes it there.
- **Echo suppression:** the `etag` and `local_hash` on the link tell a change Pandora sent from a
  change made in Google.
- **Edit scopes:** "this occurrence" patches the instance; "this and future" bounds the original
  series with UNTIL and inserts the new one, as Pandora already does locally; "all" patches the series.
- **Alerts** edited in Pandora go to Google as `popup` reminders.
- **Conflict:** when both sides changed since the last sync, the newer modification wins; the losing
  version goes to `agd011` and shows in a conflict list in the settings.
- **Scope:** writing needs `calendar` or `calendar.events`; the default Google consent already asks
  for `calendar`.
- **Tests:** push, echo and conflict against the fake Google of `GoogleCalendarSyncTests`.

**Done when:** an event created or edited in Pandora shows in Google within one cycle, with the right
recurrence and reminders, and editing both sides at once ends with the newer version and a conflict
row.

### Still to check by hand (5a)

Against a real Google account: binding calendars, an event created, edited, moved and cancelled in
Google showing within one cycle, a recurring event with one occurrence changed, an all-day event, and
a Google reminder ringing through Pandora.

### Out of scope

Attendees and RSVP, attachments, per-event colors, publishing a calendar born in Pandora to Google,
Google push notifications, and Google Tasks (phase 6).
## Phase 6 — Google Tasks sync

- `ITaskSyncProvider` reusing the binding, link and conflict machinery.
- **Done when:** the same guarantees hold for task lists and tasks.

## Beyond *(not scheduled)*

Tags shared with Notes, attaching a Note to an event, natural-language quick-add in the web UI, travel
time, location alerts, ICS import/export, CalDAV, Microsoft/Apple providers, and pulling Finances due
dates into the day view.

---

## Open questions

1. **Subtask depth.** Capped at one level (Google Tasks itself supports one level; lifting the cap
   breaks sync fidelity).
2. **Whether Finances migrates to the RRULE engine.** Not a prerequisite; revisit only if a third
   consumer of recurrence appears.
3. **Quiet hours placement.** In Channels (it owns delivery policy), reduced to `suppress` \|
   `deliver-anyway`. If Agenda ever needs "urgent alerts pierce quiet hours", the flag rides on the
   alert and Channels honours it.
