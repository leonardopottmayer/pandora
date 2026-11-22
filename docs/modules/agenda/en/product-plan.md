# Agenda Module — Roadmap (remaining work)

> **Status:** phases **1–4** (reminders, recurrence, tasks, calendar/events), the frontend and the
> Assistant tools are implemented, and two-way sync with Google Calendar (phase 5) and Google Tasks (phase 6). This file
> tracks only what is **not yet built**: the hand checks of phase 6. For
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

## Phase 6 — Google Tasks sync *(built 2026-11-13; checks by hand left)*

Two-way sync with Google task lists is built: see [Tasks §6](tasks.md#6-google-tasks). It reuses the
calendars' pattern (binding, link, pending mark, conflict log, the same job) in its own tables
(`agd012`, `agd014`); `agd011` now logs tasks too (`subject_type`).

Different from the first design: no `ITaskSyncProvider` (one provider, so no interface); Google Tasks has
no sync token, so the cursor is `updatedMin`; it takes no `If-Match`, so an update reads the task first;
Google picks the ids, so an insert whose answer is lost can duplicate a task.

Google Calendar (phase 5) was checked by hand against a real account on 2026-11-13.

### Still to check by hand

Against a real Google account, after reconnecting it so it grants `auth/tasks` (and with the Google Tasks
API enabled in the Cloud project):

- linking task lists, and their tasks (with subtasks and completed ones) showing within one cycle;
- a task created, edited, completed and deleted in Google showing in Pandora, and the same from Pandora
  to Google;
- a due time and a priority set in Pandora surviving a Google edit of the title;
- completing a recurring task, here and in Google, and the next instance reaching Google;
- editing the same task on both sides between two cycles, ending with the newer version and a row in
  the conflict list.

### Out of scope

Attendees and RSVP, attachments, per-event colors, publishing a calendar or list born in Pandora to
Google, Google push notifications, moving tasks between lists or under another parent, and task order.

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
