# Messaging — Integration Event Bus

> **Status:** Decided. Everything runs in-process, in one deployable. There is no broker and there
> are no separate workers.
> 🇧🇷 [Versão em português](../pt-BR/messaging.md)
>
> Cross-cutting document: it describes a decision no single module owns.
> Affected modules: [Channels](../../modules/channels/en/product-plan.md) ·
> [Assistant](../../modules/assistant/en/product-plan.md) ·
> [Agenda](../../modules/agenda/en/product-plan.md)

---

## 1. Where we are

Tars already abstracts the transport. `IIntegrationEventBus` has one implementation —
`InProcessIntegrationEventBus`, dispatching synchronously in a fresh DI scope — and routing happens
by the logical `IntegrationEventName`, not by the .NET type.

That is the whole mechanism. A producer publishes a fact, and the handlers registered for that name
run. Nothing else is involved.

---

## 2. The decision: in-process, one process

Pandora is a modular monolith and stays one. Modules talk through the in-process bus and through
synchronous ports; nothing crosses a network boundary to reach another module.

The reasoning is the size of the problem. This is a personal system with one user and one deploy. A
broker buys durability across process restarts and back-pressure across machines — neither of which
is a problem here — and charges infrastructure to run, a second delivery path to keep working, and a
class of failure (a message stuck in a queue nobody is watching) that is harder to debug than the
exception it replaced. The modules that would have consumed from queues are in the same process as
the ones publishing to them.

What this does **not** give up is the seam. Producers and consumers only know
`IIntegrationEventBus` and a logical event name. If the shape of the problem changes, a different
transport is a composition-root change and no handler moves. That is what makes this decision cheap
to revisit rather than a wall — see §6.

---

## 3. Asynchrony without a broker

Some work genuinely cannot happen inside the request that triggered it: an HTTP handler that must
answer quickly, or a task that takes tens of seconds. The answer is a **job in the module that owns
the work**, over that module's own table — not a queue in front of another process.

The shape is already in the codebase three times over:

| Module | Table | Job |
|---|---|---|
| Channels | `chn006_notification` | `NotificationDispatcherBackgroundService` drains what is due |
| Finances | `fin011_pending_transaction` | recurrence sweep generates what came due |
| Agenda *(planned)* | `agd00x_alert` | `AlertSweepBackgroundService` fires what is due |

The pattern each time: the caller writes a durable row in the same transaction as its state change
and returns; a `PeriodicTimer` in a `BackgroundService` picks the row up in a fresh scope, does the
work, and records the outcome. Retry with backoff, attempt counters and a dead state live in the
table, which is exactly the durability a queue would have offered — with the difference that the
state is a row you can query, correct and retry by hand.

Two rules keep this from turning into a private broker per module:

- **The table belongs to the module that does the work**, not to the one that asked for it. Channels
  owns the notification queue because Channels sends; Agenda owns the alert sweep because Agenda
  decides when.
- **A job is not a scheduler for someone else's domain.** It drains what is due in its own tables
  and publishes facts; it does not accept "run this for me later" from another module.

---

## 4. Idempotency

In-process dispatch is synchronous and delivered once, so the at-least-once dedup a broker forces is
not needed for the bus itself. It is still needed wherever a **job retries**, which is everywhere in
§3.

Where natural idempotency exists, it does the work and no extra table is needed:

- `chn006_notification` deduplicates by `(correlation_id, channel)`.
- `chn004_inbound_update` *(planned)* has `provider_update_id` as its PK — reprocessing a Telegram
  update is harmless by construction.
- `chn003_interaction` *(planned)* has `consumed_at` — a button acts once.

Every one of these is a natural key on the work itself, which is the form to prefer. A generic
processed-events table is a fallback for when no such key exists, and so far none of the modules has
needed one.

---

## 5. What does not go through the bus

**Scheduling.** "Remind me at 14:00" is not an event to be delayed; it is a row with a due time.
Scheduling lives in the owning module's table-backed job (Agenda's `AlertSweepBackgroundService`),
which publishes **at the moment it fires**. A reminder is exactly the thing whose time changes, and a
row can be rescheduled, cancelled or corrected when the user changes timezone. This is Agenda's
principle D3 and Channels' C1, and both are right.

**Request/response.** Asking Integrations for a valid token, or Channels for an audio's bytes, is a
question with an immediate answer, not a fact that happened. That is a synchronous port call.

**Reads.** No module replicates another's data via events in order to read it. Whoever needs to read
calls the port.

---

## 6. Dropped, and why

Three things were planned here and are no longer:

**A RabbitMQ broker** — two topic exchanges, one queue per logical consumer, a shared DLX. Dropped
because the problems it was brought in to solve are handled by §3 at a fraction of the cost: a fast
webhook ack becomes a row plus a job, slow Assistant work becomes a row plus a job, and a lost event
becomes a row with an attempt count. What a broker adds beyond that is infrastructure to run and
watch, which this system has no volume to justify.

**The outbox pattern** (`Messaging.Outbox`) — an EF Core table written in the same transaction as
the state change, plus a relay. It existed to solve dual-write against a broker. With no broker
there is no second commit to lose: an in-process handler runs inside the caller's flow, and the work
that must survive a crash is already a row in the module's own table.

**Extraction as a service** — Assistant first, then Channels. Dropped as a goal. The modules keep the
properties that would have made it possible, because those properties are worth having on their own
merits: each owns its `DbContext`, touches nobody else's schema, publishes POCO contracts, and talks
through ports. That is good modular design, not staging for a split.

None of this is a door that locked. Producers and consumers know a bus interface and a logical event
name, so the day a real reason appears — sustained load, a genuinely separate deploy cadence, work
that needs a machine this one is not — the transport changes at the composition root. The reason
should appear first.

---

## 7. Open questions

1. **Contract versioning.** `IntegrationEventName` already carries a version suffix
   (`identity.account-activation.v1`). Missing is the rule for when to bump it. Less pressing
   in-process, where producer and consumers deploy together, but the suffix is in the contracts and
   should mean something. Decide when the first contract actually changes.
2. **Failure visibility.** An exception in an in-process handler surfaces in the caller's flow, which
   is honest but not always where anyone is looking. The job tables carry `last_error` and a dead
   state; what is missing is one place that answers "what failed recently" across modules.
