# Messaging — Integration Event Bus

> **Status:** Plan. The in-process bus exists today; the broker does not.
> 🇧🇷 [Versão em português](../pt-BR/messaging.md)
>
> Cross-cutting document: it describes a decision no single module owns.
> Affected modules: [Channels](../../modules/channels/en/product-plan.md) ·
> [Assistant](../../modules/assistant/en/product-plan.md) ·
> [Agenda](../../modules/agenda/en/product-plan.md)

---

## 1. Where we are

Tars already abstracts the transport. `IIntegrationEventBus` has one implementation today —
`InProcessIntegrationEventBus`, dispatching synchronously in a fresh DI scope — and routing already
happens by the logical `IntegrationEventName`, not by the .NET type. That is exactly the seam a
broker-backed implementation needs.

Swapping the transport is, by construction, a composition-root change: producers and consumers do not
change.

---

## 2. Why a broker

The in-process bus is enough while all cross-module work is short and the process is one. Three
things break that:

1. **The webhook must return 200 fast.** A Telegram `callback_query` cannot wait for Agenda to
   complete a task and publish its facts. The domain work has to happen after the ack.
2. **Assistant is slow by nature.** Transcribing audio and running tool-calling on a local model
   takes seconds to tens of seconds. That cannot happen inside an HTTP request, nor while holding the
   long-polling loop.
3. **Losing an event gets expensive.** Today an exception in an in-process subscriber either brings
   down the whole flow or is swallowed. With a durable queue and dead-lettering, a failure is visible
   and reprocessable.

What the broker does **not** solve, and should not: read latency, consistency, and — above all —
scheduling (§6).

---

## 3. Topology

Two **topic** exchanges, one queue per logical consumer, one DLX for all of them.

```
pandora.events    (topic)   domain facts        identity.*  agenda.*  finances.*  notify.*
pandora.inbound   (topic)   what comes in       inbound.interaction.*  inbound.message.*
pandora.dlx       (topic)   dead letters from every queue
```

The routing key is the `IntegrationEventName`. Nothing beyond that needs per-message configuration.

| Queue | Binding | Consumer |
|---|---|---|
| `channels.dispatch` | `notify.user.requested` | Channels — delivery |
| `assistant.inbound` | `inbound.message.#` | Assistant (`prefetch=1`) |
| `agenda.interactions` | `inbound.interaction.agenda.#` | Agenda |
| `assistant.interactions` | `inbound.interaction.assistant.#` | Assistant |
| `channels.identity` | `identity.#` | Channels — security subscribers |
| `<module>.events` | `agenda.# · finances.# · notes.#` | as each one cares |

Three things that table encodes:

- **Inbound is routed, not broadcast.** `inbound.interaction.<module>.<action>` delivers to the
  owner, and the owner is a column that module itself wrote when it asked for the button (see
  [Channels §7.3](../../modules/channels/en/product-plan.md#73-interaction-tokens)). No module
  filters events that are not its own.
- **`prefetch=1` on Assistant.** Long work and a local Ollama that must not be flooded. The other
  queues use the default prefetch.
- **A single DLX.** Separate ones would give granularity nobody will look at; one place for "what
  failed" is what actually gets queried.

---

## 4. Outbox on the producer

Publishing after the commit is a second commit that can fail on its own — and the event vanishes
unnoticed. The pattern is well known and the implementation goes to Tars:

`Pottmayer.Tars.Messaging.Outbox` — an EF Core outbox table written **in the same transaction** as
the state change, plus a background relay that publishes and marks sent.

The producer still calls `IIntegrationEventBus.PublishAsync`; the difference is that the registered
implementation writes to the outbox instead of going straight to the broker. No handler changes.

---

## 5. Idempotent consumers

RabbitMQ delivery is **at-least-once**. "Create reminder" is not an operation you want twice.

Every consumer deduplicates by `EventId` — already the stable identity of the occurrence in the Tars
contract — against a per-module processed-events table, in the same transaction as the work. An event
already seen is acked and dropped.

Where natural idempotency already exists, it counts and the table is unnecessary:

- `chn004_inbound_update` has `provider_update_id` as PK — reprocessing a Telegram update is harmless
  by construction.
- `chn006_notification` deduplicates by `(correlation_id, channel)`.
- `chn003_interaction` has `consumed_at` — a button acts once.

---

## 6. What does not go on the broker

**Scheduling.** No `x-delayed-message`, no TTL + dead-letter for "remind me at 14:00". A delayed
message cannot be cancelled, rescheduled, or corrected when the user changes timezone — and a
reminder is exactly the thing whose time changes. Scheduling stays in the owning module's
table-backed scheduler (Agenda's `AlertSweepBackgroundService`), publishing **at the moment**. That
is Agenda's principle D3 and Channels' C1, and both are right.

**Request/response.** Asking Integrations for a valid token, or Channels for an audio's bytes, is a
question with an immediate answer, not a fact that happened. That is a synchronous port call,
in-process, and stays that way even after the broker lands. If those modules ever become services,
those ports become HTTP — not messages.

**Reads.** No module replicates another's data via events in order to read it. Whoever needs to read
calls the port.

---

## 7. Tars building blocks

| Project | Contents |
|---|---|
| `Pottmayer.Tars.Messaging.RabbitMq` | `IIntegrationEventBus` over a topic exchange, routing by `IntegrationEventName`; consumer host with configurable prefetch, manual ack, DLX and retry policy; re-dispatch of the deserialized message to local `IIntegrationEventHandler<T>` implementations (the "last mile" the Tars doc already describes). |
| `Pottmayer.Tars.Messaging.Outbox` | EF Core outbox table, an `IIntegrationEventBus` that writes to it, and the background relay. |

Neither knows about Pandora. Documentation goes in the Tars repository, under `docs/messaging/`.

---

## 8. When this lands

After inbound works in-process, not before. The order is:

1. [Channels C4](../../modules/channels/en/product-plan.md#phase-c4--inbound) — inbound, triage and
   routing by key, still on the in-process bus. The key-based routing is written as if a broker were
   there, because `IntegrationEventName` is the same on both transports.
2. **Transport swap** — `Messaging.RabbitMq` + `Messaging.Outbox`, `docker-compose` gains the
   service, composition root registers the new bus. No handler changes.
   **Done when:** taking the broker down for a minute loses no events.
3. [Assistant A3](../../modules/assistant/en/product-plan.md) — by the time the slow work arrives,
   the queue already exists.

Doing it in the reverse order would mean debugging routing and infrastructure at the same time.

---

## 9. Extraction as a service

The broker is not a step toward microservices; it is what makes that decision deferrable at no cost.
If and when a module leaves:

- **The first candidate is Assistant.** Long work, its own deploy cadence, possible GPU affinity, and
  it already talks only through events and one port (`IInboundMediaReader`).
- **The second is Channels**, if the public ingress justifies isolating it.
- Both already own their `DbContext` and touch nobody else's schema.

What extraction adds, and is not worth paying for beforehand: a consumer group, a decision on
replicating versus querying data over an API, and turning the synchronous ports into HTTP.

---

## 10. Open questions

1. **One `docker-compose` or a profile.** Is RabbitMQ in the development compose mandatory or
   optional? If the in-process bus stays a configuration option, you can develop without bringing the
   broker up — at the cost of two paths that must both work. Leaning: keep both, because in-process
   is what the integration tests use.
2. **Contract versioning.** `IntegrationEventName` already carries a version suffix
   (`identity.account-activation.v1`). Missing is the rule for when to bump and how two versions
   coexist on a queue. Decide when the first contract actually changes.
3. **Observability.** End-to-end correlation between the domain's `correlation_id` and the broker's
   message id. Probably a header plus a log enricher; not designed yet.
