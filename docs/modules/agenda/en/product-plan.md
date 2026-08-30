# Agenda Module — Roadmap (remaining work)

> **Status:** phases **1–4** (reminders, recurrence, tasks, calendar/events) and the frontend are
> implemented. This file now tracks only what is **not yet built** — Google sync and the Assistant
> surface. For what exists, see the module docs: [README](../README.md) · [Overview](overview.md) ·
> [Architecture](architecture.md) · [Data Model](data-model.md) · [Reminders](reminders.md) ·
> [Tasks](tasks.md) · [Calendar & Events](calendar-and-events.md) · [Alerts & Sweep](alerts-and-sweep.md) ·
> [Implementation Status](implementation-status.md).
> 🇧🇷 [Versão em português](../pt-BR/product-plan.md)
>
> Related plans: [Channels](../../channels/en/product-plan.md) ·
> [Integrations](../../integrations/en/product-plan.md) · [Assistant](../../assistant/en/product-plan.md) ·
> [Messaging](../../../architecture/en/messaging.md)

---

## Design recap (already decided & built)

The three aggregates, the principles (D1–D6), the recurrence engine, the alert model and the sweeps,
computed occurrences, overrides and the edit scopes are all documented in the files linked above and
are **built**. What remains is external sync and the Assistant catalog.

---

## Phase 5 — Google Calendar sync *(next; unblocked by Integrations I1)*

- Sync tables `agd009`–`agd012`: `calendar_binding` (local ↔ remote + direction), `sync_link`
  (`remote_id`, `etag`, hashes — prevents duplicates and echoes), `sync_cursor`, `sync_conflict`.
- `ICalendarSyncProvider` + a Google implementation, obtaining a live access token from
  [Integrations](../../integrations/en/product-plan.md) (`IExternalCredentialProvider`, already built).
- Immediate push on local writes; echo suppression by comparing hashes on the sync link;
  last-write-wins with a conflict log.
- Frontend: connect account, bind calendars, sync-now, conflict list.
- **Done when:** an event created on either side appears on the other within one pull cycle, and
  editing both sides at once resolves deterministically with a conflict row.

## Phase 6 — Google Tasks sync

- `ITaskSyncProvider` reusing the binding, link, cursor and conflict machinery.
- **Done when:** the same guarantees hold for task lists and tasks.

## Phase 7 — Assistant surface

- Register the command catalog for [Assistant](../../assistant/en/product-plan.md): `create_reminder`,
  `create_task`, `create_event`, `complete_task`, `snooze_reminder`, `whats_my_day`. The application
  commands already exist (D6); this is descriptor registration, not new domain logic.
- Relative-date resolution contract (the Assistant passes "now" and the zone; Agenda parses nothing).
- **Done when:** "remind me to call the dentist tomorrow at 9" creates the right row from Telegram.

## Cross-cutting follow-up

- **Consume the Identity time-zone default.** **Done.** Agenda still stores a `time_zone` per item
  (recurrence expands in the item's own zone), but when the caller omits it, the create handlers
  default it from Identity's `UserPreferences` through the `IUserPreferencesReader` port, falling back
  to UTC only when there is no preference. The web forms send the saved preference and the alert
  editor defaults its offset from `DefaultAlertOffsetMinutes`. `WeekStartsOn` is now honoured too, in
  all three calendar views (see below).
- **Week and day calendar views.** **Done.** A hand-rolled time grid (`WeekDayGrid`) replaced the
  placeholder: hour grid, greedy lane packing for overlapping events, all-day strip, now-indicator,
  click-to-create, and unified prev/next/today navigation. `WeekStartsOn` is honoured via a manual
  `startOfWeek` (week math) and the dayjs locale's `weekStart` (the antd month grid).

## Beyond *(not scheduled)*

Tags shared with Notes, attaching a Note to an event, natural-language quick-add in the web UI, travel
time, location alerts, ICS import/export, CalDAV, Microsoft/Apple providers, and pulling Finances due
dates into the day view.

---

## Open questions

1. **Calendar UI library vs. hand-rolled grid.** Affects only the week/day view polish.
2. **Subtask depth.** Capped at one level (Google Tasks itself supports one level; lifting the cap
   breaks sync fidelity).
3. **Whether Finances migrates to the RRULE engine.** Not a prerequisite; revisit only if a third
   consumer of recurrence appears.
4. **Quiet hours placement.** In Channels (it owns delivery policy), reduced to `suppress` \|
   `deliver_anyway`. If Agenda ever needs "urgent alerts pierce quiet hours", the flag rides on the
   alert and Channels honours it.
