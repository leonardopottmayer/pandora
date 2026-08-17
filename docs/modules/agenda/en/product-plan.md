# Agenda Module — Product Plan

> **Status:** Plan. Nothing in this document is implemented yet.
> 🇧🇷 [Versão em português](../pt-BR/product-plan.md)
>
> Related plans: [Channels](../../channels/en/product-plan.md) ·
> [Messaging](../../../architecture/en/messaging.md) ·
> [Integrations](../../integrations/en/product-plan.md) ·
> [Assistant](../../assistant/en/product-plan.md)

---

## 1. What the module does

**Agenda** is the time layer of Pandora: everything the user has to *be at*, *do*, or *be reminded
of*. One module, three distinct aggregates:

- **Events** — a personal calendar. Multiple named calendars, timed and all-day events, recurrence,
  a month/week/day/agenda UI. Two-way synced with Google Calendar.
- **Tasks** — things with a workflow. Lists, subtasks, priority, due date, `done`. Two-way synced
  with Google Tasks.
- **Reminders** — things with no workflow. "Ping me at 14:00." It fires, you acknowledge or snooze,
  it is gone. Apple Reminders semantics.

All three can raise **alerts**, which the module delivers through the
[Channels](../../channels/en/product-plan.md) module over **email**, **Telegram**, or
both, per the user's configuration. Telegram alerts carry inline buttons (*Done*, *Snooze 1h*) that
act back on the item.

### Why one module and not three

Events, tasks and reminders share too much to justify separate modules: the recurrence engine, the
alert model, the due-date sweep job, the sync-link machinery, and — critically — the user reads them
on a **single screen** ("what does my day look like"). Three modules would mean three copies of
RRULE expansion and a cross-module join for the primary read.

They are still three **aggregates** with their own tables, invariants and endpoints. If Tasks ever
outgrows the module, it leaves with its tables intact.

---

## 2. Naming and coordinates

| Thing | Value |
|---|---|
| Backend projects | `Pottmayer.Pandora.Modules.Agenda.{Abstractions,Application,Contracts,Domain,Infrastructure,Persistence,Presentation}` |
| PostgreSQL schema | `agenda` |
| Table prefix | `agdXXX_`, PK `uuid_generate_v7()` |
| API base | `/api/v{version}/agenda` |
| Frontend | `client-web/src/modules/agenda` |
| Migrations | `migrations/migrations/agenda/` |

---

## 3. Principles

1. **The alert is the only scheduling primitive.** Events, tasks and reminders do not each grow
   their own notification logic. Every "tell me about this at time T" is an `Alert` row, and one
   sweep job scans them all. *(D1)*
2. **Occurrences are computed, never stored.** A recurring event is one row plus an RRULE. Reads
   expand it in memory for the requested window. Only *deviations* from the rule get rows.
   Materializing a year of occurrences would make every edit a migration. *(D2)*
3. **The scheduling job lives here, not in Channels.** Agenda decides *when*; Channels only knows how
   to *send now*. A due time is a column on a row, so rescheduling or completing an item before it
   fires is a local update with nothing to cancel downstream — which is exactly what a reminder
   needs, being the thing whose time changes. See the
   [messaging doc](../../../architecture/en/messaging.md#5-what-does-not-go-through-the-bus). *(D3)*
4. **Time is stored absolute, displayed local, recurred in the user's zone.** `timestamptz`
   everywhere, plus an IANA zone on the item, because "every Monday at 09:00" must survive DST.
   *(D4)*
5. **The external calendar is a peer, not a master.** Pandora holds its own model; sync reconciles
   two independent stores. A user who never connects Google loses nothing. *(D5)*
6. **Everything is commandable.** Every user-facing action exists as an application command with an
   explicit parameter object, so the [Assistant](../../assistant/en/product-plan.md) can invoke it
   without any HTTP round trip or parallel code path. *(D6)*

---

## 4. Ubiquitous language

| Term | Meaning |
|---|---|
| **Calendar** | A named, colored container of events. A user has at least one (`default`). May be bound to a remote calendar. |
| **Event** | Something occupying time: start, end, all-day or timed, optional recurrence and location. |
| **Occurrence** | One materialization of a recurring event in time. Computed from the RRULE at read time. |
| **Override** | A stored deviation for a single occurrence: cancelled ("this Tuesday is off") or edited ("this one moved to 15:00"). |
| **Task list** | A named container of tasks (Apple Reminders' *list*, Todoist's *project*). |
| **Task** | Something to do: status, priority, optional due date, optional subtasks, optional recurrence. |
| **Reminder** | A ping at a moment in time. No workflow — it fires, then it is acknowledged, snoozed, or cancelled. |
| **Alert** | "Notify me about *subject* at *this offset*, over *these channels*." Polymorphic over event, task and reminder. |
| **Dispatch** | The record that one alert, for one occurrence, was handed to Channels. The idempotency key of the sweep. |
| **Sweep** | The background pass that finds due alerts within a look-ahead window and dispatches them. |
| **Calendar binding** | The declared pairing of a local calendar with a remote one, plus its direction. |
| **Sync link** | The row mapping one local entity to its remote counterpart (`remote_id`, `etag`, hashes). Prevents duplicates and echoes. |
| **Echo** | A change Pandora itself pushed, coming back on the next pull. Suppressed by comparing hashes on the sync link. |

---

## 5. Domain model

### 5.1 Aggregates

```
Calendar (agd001)
└── Event (agd002)                    ← aggregate root, references calendar_id
    └── EventOccurrenceOverride (agd003)   ← child entity

TaskList (agd004)
└── Task (agd005)                     ← aggregate root; self-referencing for subtasks

Reminder (agd006)                     ← aggregate root, standalone

Alert (agd007)                        ← polymorphic child of Event | Task | Reminder
└── AlertDispatch (agd008)            ← idempotency ledger

CalendarBinding (agd009) · SyncLink (agd010) · SyncCursor (agd011) · SyncConflict (agd012)
```

### 5.2 Schema catalog

Every table carries `user_id`, the audit quartet (`created_by/at`, `updated_by/at`) via `IAuditable`,
and is scoped to the token's user on every read — same rules as Notes and Finances.

**`agd001_calendar`**

| Column | Notes |
|---|---|
| `name`, `color`, `icon` | Display. |
| `is_default` | Exactly one per user; enforced by partial unique index. |
| `is_visible` | UI toggle; does not affect alerts. |
| `time_zone` | IANA. Defaults to the user's preference. |
| `origin` | `local` \| `external`. An `external` calendar was created by a pull and is read-mostly. |
| `archived_at` | Soft hide. |

**`agd002_event`**

| Column | Notes |
|---|---|
| `calendar_id` | FK. Moving an event between calendars is allowed. |
| `title`, `description`, `location`, `url` | `url` holds the meeting link. |
| `starts_at`, `ends_at` | `timestamptz`. For all-day, midnight in `time_zone`, end exclusive. |
| `is_all_day` | Drives rendering and sync mapping (`date` vs `dateTime`). |
| `time_zone` | IANA, per event — recurrence is expanded in this zone. |
| `rrule` | Nullable RFC 5545 `RRULE` string. Null ⇒ single occurrence. |
| `recurrence_ends_at` | Denormalized `UNTIL`/computed `COUNT` bound, so range queries can prune with an index instead of expanding every recurring row. |
| `status` | `confirmed` \| `tentative` \| `cancelled`. |
| `transparency` | `busy` \| `free`. Kept because Google round-trips it. |
| `deleted_at` | Soft delete (an inbound sync may resurrect it). |

**`agd003_event_occurrence_override`**

| Column | Notes |
|---|---|
| `event_id`, `original_starts_at` | Composite natural key — identifies *which* occurrence. |
| `is_cancelled` | The `EXDATE` case. |
| `starts_at`, `ends_at`, `title`, `description`, `location` | Nullable; non-null columns override the series for this occurrence only. |

**`agd004_task_list`** — `name`, `color`, `icon`, `is_default`, `position`, `origin`, `archived_at`.

**`agd005_task`**

| Column | Notes |
|---|---|
| `list_id`, `parent_task_id` | Subtasks are tasks. Depth capped at 1 level in the MVP (documented limit, enforced in the aggregate). |
| `title`, `notes` | |
| `due_at`, `due_has_time` | A task due "tomorrow" is not due at 00:00 — the flag drives both rendering and the default alert offset. |
| `priority` | `none` \| `low` \| `medium` \| `high`. |
| `status` | `todo` \| `in_progress` \| `done` \| `cancelled`. |
| `completed_at` | Set on `done`, cleared on reopen. |
| `rrule` | Recurring task. On completion the aggregate spawns the next instance (see §5.4). |
| `position` | Manual ordering inside the list. |
| `deleted_at` | Soft delete. |

**`agd006_reminder`**

| Column | Notes |
|---|---|
| `title`, `notes` | |
| `remind_at` | Required. The moment it fires. |
| `time_zone`, `rrule` | Recurring reminders ("every weekday at 08:00"). |
| `status` | `scheduled` \| `notified` \| `acknowledged` \| `snoozed` \| `cancelled`. |
| `snoozed_until` | Set by *Snooze*; the sweep treats it as the effective `remind_at`. |
| `acknowledged_at` | |

**`agd007_alert`**

| Column | Notes |
|---|---|
| `subject_type`, `subject_id` | `event` \| `task` \| `reminder`. No FK — polymorphic, validated in the application layer, cleaned up with the subject. |
| `offset_minutes` | Signed, relative to the subject's anchor time (event start / task due / reminder time). `0` = at the moment. `-15` = fifteen minutes before. |
| `channels` | `null` ⇒ use the user's preference for the category, in Channels. Otherwise an explicit array (`email`, `telegram`). |
| `is_enabled` | |

A `Reminder` is created with one alert at `offset_minutes = 0`. Events and tasks default to the
user's preferred lead time (a preference, not a hard-coded 15).

**`agd008_alert_dispatch`**

| Column | Notes |
|---|---|
| `alert_id`, `occurrence_starts_at` | Unique together. This is what makes the sweep idempotent across restarts, clock skew and overlapping windows. |
| `dispatched_at`, `correlation_id` | `correlation_id` is the same value handed to Channels, so a delivery can be traced end to end. |
| `channels_resolved` | What was actually requested, after applying user defaults and quiet hours. |

**`agd009_calendar_binding`** — `calendar_id`, `external_account_id` (Integrations), `remote_calendar_id`,
`direction` (`bidirectional` \| `pull_only` \| `push_only`), `is_enabled`, `last_synced_at`.
The same shape is reused for task lists (`subject_type` discriminator) so Google Tasks needs no second table.

**`agd010_sync_link`** — `provider`, `external_account_id`, `local_kind` (`event` \| `task` \| `calendar` \| `task_list`),
`local_id`, `remote_id`, `remote_etag`, `remote_updated_at`, `local_hash`, `last_synced_at`.
Unique on `(provider, external_account_id, remote_id)` **and** on `(local_kind, local_id, external_account_id)`.

**`agd011_sync_cursor`** — `external_account_id`, `remote_calendar_id`, `sync_token`, `last_full_sync_at`,
`consecutive_failures`. A `410 Gone` from Google clears the token and forces a full resync.

**`agd012_sync_conflict`** — append-only log of last-write-wins resolutions: what was overwritten,
which side won, the discarded payload as JSON. Never surfaced as a queue to act on; it exists so that
"Google ate my edit" is answerable.

### 5.3 Recurrence

A pragmatic RFC 5545 subset, stored as the raw `RRULE` string so that sync with Google is a copy,
not a lossy translation.

**Supported:** `FREQ=DAILY|WEEKLY|MONTHLY|YEARLY`, `INTERVAL`, `BYDAY` (including ordinals like
`2TU`, `-1FR`), `BYMONTHDAY`, `BYMONTH`, `COUNT`, `UNTIL`, `WKST`.

**Not supported (rejected on write, preserved read-only on inbound sync):** `BYSETPOS`, `BYWEEKNO`,
`BYYEARDAY`, `BYHOUR`/`BYMINUTE`/`BYSECOND`.

An inbound event whose rule uses an unsupported part is stored verbatim, flagged
`recurrence_unsupported`, rendered as read-only, and **never pushed back** — Pandora must not
downgrade a rule it cannot represent.

Expansion lives in `Domain/Recurrence/`: `RecurrenceRule.Parse(string)` and
`Expand(DateTimeOffset from, DateTimeOffset to, TimeZoneInfo zone)`. It is a pure function, DST-aware
(expansion happens on local wall-clock time and is then converted back), and covered by a table-driven
test suite before anything else in the module depends on it.

> **Deliberate duplication:** Finances already has its own recurrence engine, simpler and
> non-RRULE. They stay separate. Unifying them is a possible future refactor, not a prerequisite —
> and if it happens, the RRULE engine is the survivor and moves to Tars.

### 5.4 Behaviours worth naming

- **Completing a recurring task** closes the current instance (`done`, `completed_at`) and creates
  the next one from the RRULE, carrying over notes, priority, list and alerts. Two rows, not one
  mutable row, so history survives.
- **Snoozing a reminder** sets `snoozed_until` and `status = snoozed`. The sweep reads
  `COALESCE(snoozed_until, remind_at)`. A snooze never creates a new dispatch row for the original
  occurrence — it creates one for the snoozed time, keeping the ledger honest.
- **Editing one occurrence** of a series writes an override row. Editing "this and all future"
  splits the series: the original gets `UNTIL` at the split point, and a new event starts from there
  (the standard iCalendar approach, and what Google expects).
- **Deleting a calendar** is refused while it holds live events, mirroring the Finances delete
  guards. Archive it instead.

---

## 6. The alert sweep

One `AlertSweepBackgroundService`, modelled on the existing
`NotificationDispatcherBackgroundService`, running on a short interval (default 60s).

```
every tick:
  window = [now - grace, now + lookahead]        # grace covers downtime, lookahead is 0 by default
  for each enabled alert whose subject is live:
      anchors = expand(subject, window)          # 1 anchor for non-recurring, N for recurring
      for each anchor:
          fire_at = anchor + offset_minutes
          if fire_at not in window: continue
          if dispatch exists for (alert_id, anchor): continue    # idempotent
          channels = resolve(alert.channels, user defaults, quiet hours)
          if channels is empty: record dispatch as suppressed; continue
          publish NotifyUserRequested(correlation_id, user_id, category, template,
                                      payload, buttons)
          insert dispatch row
```

Everything happens in one unit of work per alert, so a crash mid-tick replays cleanly on the next
one. `grace` (default 15 minutes) means a laptop that was asleep still delivers the reminder it
missed, once, marked late — rather than silently swallowing it or flooding on wake.

The sweep publishes **`NotifyUserRequested`** (a
[Channels contract](../../channels/en/product-plan.md#81-work-coming-in)) with the category, template
key, payload and — the part only Agenda knows — the **buttons**: `(owner_module: "agenda", action,
payload)`. The `correlation_id` is the dispatch id, so a delivery is traceable end to end.

Agenda picks neither channel, address nor wording: the category (`agenda.reminder`, `agenda.task`,
`agenda.event`) resolves channels from the user's preferences, and per-channel template variants live
in Channels. What Agenda declares is *what can be done with the message*, because that is its domain.

> The `identity.*` subscribers in Channels keep the older path — the producer publishes a fact and
> Channels maps it to a template. Both paths coexist, and the rule for picking one is simple:
> **whoever owns the buttons owns the `NotifyUserRequested`**. A security notification has no
> buttons.

---

## 7. Inbound actions from Telegram

When Channels sends a message with buttons, it registers each button in `chn003_interaction` with the
`owner_module` Agenda declared. On the click it resolves that id and publishes with the routing key
**`inbound.interaction.agenda.<action>`** — handled only by Agenda's subscriber. There is no broadcast
and Agenda filters nothing.

The contract received is `InboundInteractionReceived(userId, channel, ownerModule, action, payload,
sourceCorrelationId)`. Agenda subscribes and maps:

| Action | Effect |
|---|---|
| `task_done` | Task → `done`. Reminder → `acknowledged`. |
| `snooze_10m` / `snooze_1h` / `snooze_tomorrow` | Reminder → `snoozed` with the new time. |
| **Open** | A deep link into the web client — a URL button, producing neither interaction nor event. |

Three guarantees come from Channels and need no reimplementation here:

- **Authenticity.** The `user_id` comes from the interaction row, not from the client. Agenda
  resolves the subject from the `payload` it wrote itself on the way out.
- **Single use and expiry.** Yesterday's button does not act today, and a double click arrives once.
- **Correlation.** `sourceCorrelationId` ties the click to the dispatch that produced it.

Even so, acting on an already-acted item is a no-op with a friendly reply, not an error — domain
idempotency stays Agenda's responsibility.

---

## 8. Sync with external providers

### 8.1 Shape

```
Agenda.Domain/Ports/Sync/
    ICalendarSyncProvider     ListChanged / Create / Update / Delete / ResolveCalendars
    ITaskSyncProvider         same shape for lists and tasks

Agenda.Infrastructure/Sync/Google/
    GoogleCalendarSyncProvider     ← Calendar API v3, syncToken-based incremental list
    GoogleTasksSyncProvider        ← Tasks API v1
```

Credentials never live here. The provider asks
[Integrations](../../integrations/en/product-plan.md) for a valid access token via
`IExternalCredentialProvider`, which refreshes transparently. Adding CalDAV or Microsoft later means one
new folder under `Sync/`, plus a provider registration — no change in Agenda's domain.

### 8.2 Pull

A `SyncBackgroundService` runs per connected account on an interval (default 5 min):

1. Read the `sync_cursor` for the bound remote calendar; call the provider's incremental list.
2. For each remote change, find the `sync_link` by `remote_id`.
   - **No link** → create locally, write the link.
   - **Link exists, `remote_etag` unchanged** → nothing to do.
   - **Link exists, remote changed** → conflict check (§8.4), then apply.
   - **Remote deleted** → soft-delete locally, drop the link.
3. Store the new `sync_token`. A `410 Gone` clears it and schedules a full resync.

### 8.3 Push

Local writes push **immediately**, not on the next tick — the user expects an event created in
Pandora to be on their phone before they put it down. The application command commits the local
change, then enqueues a push job; the job is retried with backoff and is idempotent on the
`sync_link`. A push failure never rolls back the local write; it marks the link `pending_push` and
the sweep retries it.

### 8.4 Conflict: last write wins

When both sides changed since `last_synced_at`, the newer `updated_at` wins, whole-entity. The loser
is written to `agd012_sync_conflict` with its full payload. No per-field merge — with a single human
user, the cost of a rare lost edit is far below the cost of a merge engine, and the conflict log
makes it recoverable by hand.

### 8.5 Echo suppression

Every write stores `local_hash` (a stable hash of the synced fields) on the link. On pull, if the
incoming remote payload hashes to the stored `local_hash`, the change is Pandora's own echo and is
skipped without touching `updated_at`. Without this, two systems with last-write-wins ping-pong
forever.

### 8.6 Explicit non-goals

Attendees and invitations, free/busy queries of other people, shared calendars, and Google Meet
creation. Pandora is single-user; an event has no guest list. Inbound events carrying attendees keep
them as opaque JSON so a round-trip does not destroy them.

---

## 9. API surface (sketch)

```
GET    /agenda/calendars                       POST /agenda/calendars
PATCH  /agenda/calendars/{id}                  DELETE /agenda/calendars/{id}

GET    /agenda/events?from=&to=&calendarIds=   → expanded occurrences, not rows
POST   /agenda/events
PATCH  /agenda/events/{id}?scope=this|this-and-future|all
DELETE /agenda/events/{id}?scope=...

GET    /agenda/task-lists                      POST /agenda/task-lists
GET    /agenda/tasks?listId=&status=&due=      POST /agenda/tasks
PATCH  /agenda/tasks/{id}                      POST /agenda/tasks/{id}/complete
POST   /agenda/tasks/{id}/reopen               DELETE /agenda/tasks/{id}

GET    /agenda/reminders?status=&from=&to=     POST /agenda/reminders
POST   /agenda/reminders/{id}/acknowledge      POST /agenda/reminders/{id}/snooze
DELETE /agenda/reminders/{id}

POST   /agenda/{subjectType}/{id}/alerts       DELETE /agenda/alerts/{id}

GET    /agenda/today                           → the unified day view: events + due tasks + reminders
GET    /agenda/upcoming?days=7

GET    /agenda/sync/bindings                   POST /agenda/sync/bindings
POST   /agenda/sync/run                        → manual "sync now"
GET    /agenda/sync/conflicts
```

`GET /agenda/today` is the module's headline read and the Assistant's most-used query. It is a
single handler that expands occurrences, merges the three sources, and sorts by time.

---

## 10. Frontend

`client-web/src/modules/agenda`, React + TanStack Query + antd, matching Notes and Finances.

| Screen | Contents |
|---|---|
| **Calendar** | Month / week / day / agenda views, drag to move and resize, click-drag to create, calendar visibility toggles in a sidebar. |
| **Tasks** | Lists in a sidebar; grouped by due (Overdue / Today / This week / Later / No date); inline complete, subtask expansion, drag to reorder. |
| **Reminders** | A flat, chronological list with snooze/acknowledge inline. |
| **Today** | The landing screen: the merged day, from `GET /agenda/today`. |
| **Settings** | Default calendar and list, default alert offsets, per-channel preferences, connected accounts and calendar bindings, conflict log. |

The calendar grid is the one piece with no precedent in the codebase. Decision deferred to
implementation, but the default is a lightweight headless approach over a heavy calendar library, so
the recurrence semantics stay ours.

---

## 11. Dependencies on other modules

| Dependency | Why | Where it is planned |
|---|---|---|
| **Identity — user time zone** | Nothing in this module is correct without it. `UserPreferences` currently holds only `Theme` and `Language`; it needs an IANA `TimeZone`, plus `DefaultAlertOffsetMinutes` and `WeekStartsOn`. | §12, Phase 0 |
| **Channels — multi-channel** | Alerts must reach Telegram, addresses must be per-user, and inline buttons must come back. | [Channels plan](../../channels/en/product-plan.md) |
| **Integrations — OAuth** | Google tokens, refreshed transparently. | [Integrations plan](../../integrations/en/product-plan.md) |
| **Assistant — command catalog** | Agenda registers its commands; the LLM calls them. | [Assistant plan](../../assistant/en/product-plan.md) |

---

## 12. Roadmap

Phases are ordered so that something useful lands early and nothing is built twice.

### Phase 0 — Foundation *(blocking, mostly outside this module)*
- `UserPreferences`: add `TimeZone` (IANA), `WeekStartsOn`, `DefaultAlertOffsetMinutes`; migration,
  DTO, endpoint, settings UI.
- Channels: rename, multi-channel refactor, Telegram sender, per-user channel bindings and inbound
  with interaction tokens. See the Channels plan, phases C1–C4.
- Tars: `Communication.Telegram.Abstractions` + Bot API implementation.
- **Done when:** a test can send the same notification to email and Telegram for a linked user.

### Phase 1 — Module scaffold and Reminders
- Seven projects, `agenda` schema, DI wiring, module registration.
- `Reminder`, `Alert`, `AlertDispatch`; sweep job; publishing `NotifyUserRequested` with declared
  buttons, and the template variants in Channels.
- Subscriber bound to `inbound.interaction.agenda.#`; `task_done` and `snooze_*`
  handlers.
- CRUD endpoints, acknowledge, snooze; inline buttons wired end to end.
- Frontend: Reminders screen + settings.
- **Done when:** a reminder created in the browser buzzes the phone at the right minute, and
  *Snooze 1h* from Telegram moves it.

### Phase 2 — Recurrence engine
- `RecurrenceRule` parse and expand, table-driven tests including DST boundaries and `-1FR`-style
  ordinals.
- Recurring reminders; per-occurrence dispatch idempotency proven under restart.
- **Done when:** "every weekday at 08:00" fires exactly once per weekday across a DST change.

### Phase 3 — Tasks
- `TaskList`, `Task`, subtasks, priority, due with/without time, complete/reopen, recurring tasks.
- Alerts on tasks; overdue behaviour.
- Frontend: Tasks screen with grouping and inline completion.
- **Done when:** a recurring weekly task completed today reappears next week with its alerts.

### Phase 4 — Calendar and events
- `Calendar`, `Event`, overrides, the `this / this-and-future / all` edit scopes.
- Range query with in-memory expansion; `GET /agenda/today`.
- Frontend: month/week/day views.
- **Done when:** a recurring event edited "this and future" splits correctly and the day view agrees.

### Phase 5 — Google Calendar sync
- Integrations module (see its plan) delivering a live access token.
- `ICalendarSyncProvider` + Google implementation, cursors, links, immediate push, echo suppression,
  last-write-wins with a conflict log.
- Frontend: connect account, bind calendars, sync-now, conflict list.
- **Done when:** an event created on either side appears on the other within one pull cycle, and
  editing both sides at once resolves deterministically with a conflict row.

### Phase 6 — Google Tasks sync
- `ITaskSyncProvider` reusing the binding, link, cursor and conflict machinery.
- **Done when:** the same guarantees hold for task lists and tasks.

### Phase 7 — Assistant surface
- Register the command catalog: `create_reminder`, `create_task`, `create_event`, `complete_task`,
  `snooze_reminder`, `whats_my_day`.
- Relative-date resolution contract (the Assistant passes "now" and the zone; Agenda parses nothing).
- **Done when:** "remind me to call the dentist tomorrow at 9" creates the right row from Telegram.

### Beyond
Tags shared with Notes, attaching a Note to an event, natural-language quick-add in the web UI,
travel time, location alerts, ICS import/export, CalDAV, Microsoft/Apple providers, and pulling
Finances due dates into the day view.

---

## 13. Open questions

1. **Calendar UI library vs. hand-rolled grid.** Deferred to Phase 4; affects nothing before it.
2. **Subtask depth.** Capped at one level in the MVP. Unlimited nesting is a UI problem more than a
   model problem, and Google Tasks itself only supports one level — lifting the cap would break sync
   fidelity.
3. **Whether Finances migrates to the RRULE engine.** Not a prerequisite. Revisit only if a third
   consumer of recurrence appears.
4. **Quiet hours placement.** In Channels (it owns delivery policy), reduced to `suppress` \|
   `deliver_anyway` — holding until morning is scheduling, and scheduling belongs here. If
   Agenda ever needs "urgent alerts pierce quiet hours", the flag rides on the alert and
   Channels honours it.
