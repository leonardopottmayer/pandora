# Overview — Boundary & Principles

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Data Model](data-model.md)

---

## 1. What the module does

**Channels** owns the whole conversation with the user, in both directions:

- **Outbound.** A durable notification queue (`Pending → Sending → Sent`, exponential backoff,
  `MaxAttempts`, `Dead` dead-letter) over **email** and **Telegram**, with per-channel rendering from
  one template key.
- **Addressing.** It knows *who* a notification is for — a user, not just an address — and resolves
  that user's usable channels on its own.
- **Delivery policy.** Per-user, per-category preferences decide which channels a given kind of
  notification goes out on. One request fans out into N independent rows.
- **Inbound.** It receives Telegram updates: account linking, inline-button callbacks, text messages
  and voice notes, recorded idempotently and **routed to the owning module**.

It was renamed from `Notifications` once half of what it does stopped being a notification — it hosts
ingress, handles `/start`, stores chat ids and receives audio.

## 2. The boundary

> **Channels:** Pandora talks *to* the user. **Integrations:** Pandora calls a third party *as* the user.

A Telegram chat id is an **address** where Pandora reaches the user, and a bot token is a
**deployment** credential — both belong here, not in [Integrations](../../integrations/en/overview.md).
What does **not** belong here is the *processing* of what the user sent — transcribing, interpreting,
executing a command — that lives in [Assistant](../../assistant/en/product-plan.md). Channels routes
the raw inbound event; Assistant makes sense of it.

### One module, not two

Splitting "transport" from "delivery policy" was evaluated and **rejected**: fan-out is a join across
preferences (`chn005`) and enabled/verified addresses (`chn001`) that must fit in one transaction; the
transport port has a single caller (the dispatcher); and interaction buttons are born from
notifications (`chn003` has an FK to the queue row). The seam is real but internal — it lives in
namespaces (`Delivery` / `Ingress` / `Addressing`), not in separate `.csproj` files.

## 3. Core principles

1. **Channels sends now; it does not schedule.** No `ScheduledFor`, no cancellation API. Whoever wants
   delivery at 14:00 calls at 14:00. *(C1)*
2. **The caller names a user and an intent, not an address.** Address resolution, channel selection and
   opt-outs are delivery policy, and delivery policy lives here. *(C2)*
3. **One request becomes N notifications.** "Email and Telegram" are two rows sharing a `group_id` —
   independent retry, independent failure, honest status. *(C3)*
4. **Channels are ports.** Adding WhatsApp is an `IChannelTransport` implementation plus a template
   variant. No `switch` in the dispatcher. *(C4)*
5. **Inbound is classified structurally, never semantically.** The module resolves an id in a table and
   reads the column the owning module wrote; it never interprets what an action means. *(C5)*
6. **Rendering happens at enqueue, not at send.** What went out is stored; retry resends byte-for-byte
   the same content; changing a template tomorrow does not rewrite history. *(C6)*

## 4. Ubiquitous language (glossary)

| Term | Meaning |
|---|---|
| **Channel** | A delivery medium: `email` or `telegram`. |
| **User channel** (`chn001`) | Where a user can be reached on a channel — an address that is usable only when both **verified** and **enabled**. |
| **Link token** (`chn002`) | The single-use, short-lived handshake that ties a Telegram chat to an account. A chat id is *never* accepted from the client. |
| **Notification** (`chn006`) | One durable queue row: an addressed, already-rendered message with its own retry/status. |
| **Group** | The N rows one request fans out into (email + Telegram), sharing a `group_id`, read as one notification. |
| **Category** | The kind of a notification (`agenda.reminder`, `identity.security`, …) — the key delivery policy is keyed on. |
| **Preference** (`chn005`) | The ordered list of channels a category goes out on for a user. Empty ⇒ muted. `identity.*` is mandatory and ignores it. |
| **Template** | A file per `(key, channel, locale)`, validated at startup. Renders subject/body (email) or structured payload (Telegram). |
| **Interaction** (`chn003`) | A registered inline button and its route home: `(user, owner_module, action, payload)` behind a single id that fits a 64-byte Telegram callback. |
| **Inbound update** (`chn004`) | Every update the bot received, recorded before processing; the provider's `update_id` makes reprocessing harmless. |
| **Triage / classification** | The structural sort of an inbound update into `Interaction \| Command \| Message \| Discarded`. |

## 5. Scope

### In scope (implemented — see [Implementation Status](implementation-status.md))

The `channels` schema (`chn001`–`chn007`); email + Telegram transports; per-channel file templates
with startup validation; the durable queue with fan-out, dedup by `(correlation_id, channel)`, retry
and dead-lettering; per-user/per-category preferences; global quiet hours (`chn007`, in the user's
zone, `suppress`/`deliver-anyway`); the Telegram linking handshake; long-polling inbound with triage;
interaction buttons routed to owners; inbound media reading; delivery history; the daily raw-payload
retention purge; and OpenTelemetry metrics (queue depth, dispatch latency, per-channel outcomes,
discarded updates).

### Out of scope / future (see [product-plan.md](product-plan.md))

| Feature | Status |
|---|---|
| **Webhook driver** | Deferred — long polling covers ingress everywhere; earns its place once the homelab has public HTTPS. |
| **Manual retry of a dead row** | Not planned while dead-letters are rare and inspectable. |
| **Finances notification categories** | Finances events not published yet; a small follow-up once it opts in. |
