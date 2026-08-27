# Channels Module

> The owner of the conversation with the user — in both directions, across every channel — inside the
> Pandora modular monolith.
> **Language:** English is the primary documentation. 🇧🇷 [Versão em português](pt-BR/README.md).

The **Channels** module (formerly `Notifications`) owns how Pandora **talks to the user**: a durable
outbound queue over **email** and **Telegram**, per-user delivery policy, and inbound traffic —
account linking, inline-button callbacks, text messages and voice notes — routed back to the module
that owns it.

The boundary that defines it, in one line:

> **Channels:** Pandora talks *to* the user. **[Integrations](../integrations/README.md):** Pandora
> calls a third party *as* the user.

Two guiding rules: **Channels sends now, it does not schedule** (whoever wants delivery at 14:00 calls
at 14:00), and **inbound is classified structurally, never semantically** — the module resolves an id
in a table and reads the column the owning module wrote; it never interprets what an action *means*.

---

## How this documentation is organized

Start with the **Overview** for the boundary and vocabulary, then read the topic you need.

| # | Document | What it covers |
|---|---|---|
| 1 | [Overview](en/overview.md) | What the module does, the Channels/Integrations boundary, principles, ubiquitous language, scope |
| 2 | [Architecture](en/architecture.md) | Project layout, the Delivery/Ingress/Addressing seam, domain building blocks, decisions, the Tars Telegram block |
| 3 | [Data Model](en/data-model.md) | Schema catalog (`chn001`–`chn006`): columns, constraints, indexes |
| 4 | [Outbound & Templates](en/outbound-and-templates.md) | Enqueue, fan-out, per-channel rendering, template tree, buttons, the dispatcher & retry |
| 5 | [Inbound & Linking](en/inbound-and-linking.md) | The Telegram handshake, long polling, triage, interactions, media, routing back to owners |
| 6 | [API Reference](en/api-reference.md) | Every endpoint under `/api/v{n}/channels` |
| 7 | [Implementation Status](en/implementation-status.md) | What is built vs. planned |

The forward-looking roadmap (phase-C5 remainder and beyond) lives in [product-plan.md](en/product-plan.md).

---

## Quick facts

- **Backend:** `Pottmayer.Pandora.Modules.Channels.*` (.NET 10, DDD, CQRS-style commands/queries).
- **Schema:** PostgreSQL schema `channels`, tables prefixed `chnXXX_`, PK `uuid_generate_v7()`.
- **Frontend:** a **Notifications** section under settings (channels, test send, preferences, delivery history).
- **API base:** `/api/v{version}/channels`, authenticated and scoped to the token's user.
- **Migrations:** `migrations/migrations/channels/`.
- **Transports:** email via `Pottmayer.Tars.Communication.Email.MailKit`; Telegram via
  `Pottmayer.Tars.Communication.Telegram`.
