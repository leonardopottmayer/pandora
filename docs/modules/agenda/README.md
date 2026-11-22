# Agenda Module

> The time layer of Pandora — everything the user has to *be at*, *do*, or *be reminded of* — inside
> the modular monolith.
> **Language:** English is the primary documentation. 🇧🇷 [Versão em português](pt-BR/README.md).

The **Agenda** module is one module with three distinct aggregates:

- **Events** — a personal calendar: multiple named calendars, timed and all-day events, recurrence,
  a month/week/day/agenda UI.
- **Tasks** — things with a workflow: lists, subtasks, priority, due date, `done`.
- **Reminders** — things with no workflow: "ping me at 14:00"; it fires, you acknowledge or snooze,
  it is gone.

All three raise **alerts**, delivered through [Channels](../channels/README.md) over email and/or
Telegram, with inline buttons (*Done*, *Snooze 1h*) that act back on the item.

Two rules define the module: **the alert is the only scheduling primitive** (every "tell me at time T"
is an alert row scanned by a background sweep), and **occurrences are computed, never stored** (a
recurring series is one row plus an RRULE, expanded on read; only *deviations* get rows).

---

## How this documentation is organized

Start with the **Overview** for the business picture and vocabulary, then read the topic you need.

| # | Document | What it covers |
|---|---|---|
| 1 | [Overview](en/overview.md) | The three aggregates, principles, ubiquitous language, scope |
| 2 | [Architecture](en/architecture.md) | Project layout, aggregates & value objects, the recurrence engine, the sweeps, decisions |
| 3 | [Data Model](en/data-model.md) | Schema catalog (`agd001`–`agd008`, sync tables reserved) |
| 4 | [Reminders](en/reminders.md) | Single-shot vs. recurring, the per-occurrence dispatch ledger, acknowledge/snooze |
| 5 | [Tasks](en/tasks.md) | Lists, subtasks, priority, due dates, recurrence materialization, complete/reopen, alerts |
| 6 | [Calendar & Events](en/calendar-and-events.md) | Calendars, computed occurrences, overrides, the this/this-and-future/all edit scopes, Today |
| 7 | [Alerts & Sweep](en/alerts-and-sweep.md) | The polymorphic alert, the sweeps, dispatch idempotency, grace/look-ahead, inline buttons |
| 8 | [API Reference](en/api-reference.md) | Every endpoint under `/api/v{n}/agenda` |
| 9 | [Implementation Status](en/implementation-status.md) | What is built vs. planned |

The forward-looking roadmap (Google sync and the Assistant surface) lives in [product-plan.md](en/product-plan.md).

---

## Quick facts

- **Backend:** `Pottmayer.Pandora.Modules.Agenda.*` (.NET 10, DDD, CQRS-style commands/queries).
- **Schema:** PostgreSQL schema `agenda`, tables prefixed `agdXXX_`, PK `uuid_generate_v7()`.
- **Frontend:** `client-web/src/modules/agenda` (Today, Reminders, Tasks, Calendar).
- **API base:** `/api/v{version}/agenda`, authenticated and scoped to the token's user.
- **Migrations:** `migrations/migrations/agenda/`.
- **Alerts** are delivered via [Channels](../channels/README.md) (`NotifyUserRequested` with buttons).
