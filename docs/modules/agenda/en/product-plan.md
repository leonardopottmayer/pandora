# Agenda Module — Roadmap (remaining work)

> **Status:** phases **1–4** (reminders, recurrence, tasks, calendar/events), the frontend and the
> Assistant tools are implemented, and two-way Google Calendar sync (phase 5). This file tracks only what is
> **not yet built**: the hand checks of phase 5 and Google Tasks. For
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

## Phase 5 — Google Calendar sync *(built 2026-11-13; checks by hand left)*

Two-way sync with Google calendars is built: see
[Calendar & Events §6](calendar-and-events.md#6-google-calendar). The decisions it followed: the user
picks Google calendars, each becoming its own Pandora calendar; Google's reminders become alerts and
alerts become popup reminders; polling every 2 minutes with `syncToken` (push notifications need public
HTTPS); the newer edit wins a conflict and the other goes to a conflict list.

Different from the first design: no `local_hash` (the pending mark and the etag are enough); an event
cannot move into or out of a Google calendar; the push runs at the start of the next cycle, not right
after the commit; a calendar's direction comes from the account's access to it.

### Still to check by hand

Against a real Google account:

- **Reading:** binding calendars; an event created, edited, moved and cancelled in Google showing within
  one cycle; a recurring event with one occurrence changed; an all-day event; a Google reminder ringing
  through Pandora.
- **Writing:** an event created, edited and deleted in Pandora showing in Google within one cycle, with
  its recurrence and reminders; "this occurrence" and "this and future" on a series; editing the same
  event on both sides between two cycles, ending with the newer version and a row in the conflict list;
  a calendar shared read-only staying read-only.

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
