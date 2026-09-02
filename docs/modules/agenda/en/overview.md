# Overview — Business & Principles

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Data Model](data-model.md)

---

## 1. What the module does

**Agenda** is the time layer of Pandora: everything the user has to *be at*, *do*, or *be reminded
of*. One module, three distinct aggregates:

- **Events** — a personal calendar. Multiple named calendars, timed and all-day events, recurrence,
  a month/week/day/agenda UI.
- **Tasks** — things with a workflow. Lists, subtasks, priority, due date, `done`.
- **Reminders** — things with no workflow. "Ping me at 14:00." It fires; you acknowledge or snooze;
  it is gone. Apple Reminders semantics.

All three can raise **alerts**, which the module delivers through
[Channels](../../channels/en/overview.md) over email, Telegram, or both, per the user's configuration.
Telegram alerts carry inline buttons (*Done*, *Snooze 1h*) that act back on the item.

### Why one module and not three

Events, tasks and reminders share too much to justify separate modules: the recurrence engine, the
alert model, the due-date sweep, and — critically — the user reads them on a **single screen** ("what
does my day look like"). Three modules would mean three copies of RRULE expansion and a cross-module
join for the primary read. They remain three **aggregates** with their own tables, invariants and
endpoints; if Tasks ever outgrows the module, it leaves with its tables intact.

## 2. Core principles

1. **The alert is the only scheduling primitive.** Events, tasks and reminders do not each grow their
   own notification logic. Every "tell me about this at time T" is an `Alert` row, scanned by a sweep.
   *(D1)*
2. **Occurrences are computed, never stored.** A recurring event is one row plus an RRULE; reads
   expand it in memory for the requested window. Only *deviations* from the rule get rows.
   Materializing a year of occurrences would make every edit a migration. *(D2)*
3. **The scheduling job lives here, not in Channels.** Agenda decides *when*; Channels only knows how
   to *send now*. A due time is a column on a row, so rescheduling or completing an item before it
   fires is a local update with nothing to cancel downstream. *(D3)*
4. **Time is stored absolute, displayed local, recurred in the user's zone.** `timestamptz`
   everywhere, plus an IANA zone on the item, because "every Monday at 09:00" must survive DST. *(D4)*
5. **The external calendar is a peer, not a master.** Pandora holds its own model; sync (when it
   lands) reconciles two independent stores. A user who never connects Google loses nothing. *(D5)*
6. **Everything is commandable.** Every user-facing action is an application command with an explicit
   parameter object, so [Assistant](../../assistant/en/product-plan.md) can invoke it without any HTTP
   round trip or parallel code path. *(D6)*

## 3. Ubiquitous language

| Term | Meaning |
|---|---|
| **Calendar** (`agd001`) | A named, colored container of events. A user has at least one (`default`). |
| **Event** (`agd002`) | Something occupying time: start, end, all-day or timed, optional recurrence and location. |
| **Occurrence** | One materialization of a recurring event in time. Computed from the RRULE at read time. |
| **Override** (`agd003`) | A stored deviation for a single occurrence: cancelled ("this Tuesday is off") or edited ("this one moved to 15:00"). |
| **Task list** (`agd004`) | A named container of tasks (Apple Reminders' *list*, Todoist's *project*). |
| **Task** (`agd005`) | Something to do: status, priority, optional due date, optional subtasks, optional recurrence. |
| **Reminder** (`agd006`) | A ping at a moment in time. No workflow — it fires, then is acknowledged, snoozed, or cancelled. |
| **Alert** (`agd007`) | "Notify me about *subject* at *this offset*, over *these channels*." Polymorphic over event, task and reminder. |
| **Dispatch** (`agd006x` / `agd008`) | The record that one alert (or recurring reminder), for one occurrence, was handed to Channels. The idempotency key of the sweep. |
| **Sweep** | The background pass that finds due alerts within a look-ahead window and dispatches them. |
| **RRULE** | An RFC 5545 subset stored verbatim on the item, so a future Google sync is a copy, not a lossy translation. |

## 4. Scope

### In scope (implemented — see [Implementation Status](implementation-status.md))

The `agenda` schema (`agd001`–`agd008`); reminders (single-shot and recurring) with a per-occurrence
dispatch ledger; the RRULE recurrence engine (parse + expand, DST-aware); tasks with lists, subtasks,
priority, due dates, complete/reopen, recurring materialization and alerts; calendars and events with
computed occurrences, overrides and the this / this-and-future / all edit scopes; the polymorphic
alert with three background sweeps; the `GET /agenda/today` unified read; inline Telegram buttons
routed back from Channels; and the frontend (Today, Reminders, Tasks, Calendar).

### Out of scope / future (see [product-plan.md](product-plan.md))

| Feature | Status |
|---|---|
| **Google Calendar sync** (phase 5) | Not implemented — sync tables (`agd009`–`agd012`), providers, cursors, conflict log absent. Depends on [Integrations](../../integrations/en/overview.md). |
| **Google Tasks sync** (phase 6) | Not implemented. |
| **Assistant command catalog** (phase 7) | Not implemented — commands exist and are commandable, but the descriptor registration for Assistant is not wired. |
| **Beyond** | Note ↔ event links, natural-language quick-add, travel time, ICS/CalDAV, Microsoft/Apple providers, Finances due dates in the day view. |
