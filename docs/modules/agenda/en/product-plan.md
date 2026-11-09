# Agenda Module — Roadmap (remaining work)

> **Status:** phases **1–4** (reminders, recurrence, tasks, calendar/events), the frontend and the
> Assistant tools are implemented. This file tracks only what is **not yet built**: Google sync. For
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

## Phase 5 — Google Calendar sync *(next; unblocked by Integrations I1)*

- Sync tables `agd009`–`agd012`: `calendar_binding` (local ↔ remote + direction), `sync_link`
  (`remote_id`, `etag`, hashes — prevents duplicates and echoes), `sync_cursor`, `sync_conflict`.
- `ICalendarSyncProvider` + a Google implementation, obtaining a live access token from
  [Integrations](../../integrations/README.md) (`IExternalCredentialProvider`, already built).
- Immediate push on local writes; echo suppression by comparing hashes on the sync link;
  last-write-wins with a conflict log.
- Frontend: connect account, bind calendars, sync-now, conflict list.
- **Done when:** an event created on either side appears on the other within one pull cycle, and
  editing both sides at once resolves deterministically with a conflict row.

## Phase 6 — Google Tasks sync

- `ITaskSyncProvider` reusing the binding, link, cursor and conflict machinery.
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
