# Channels Module — Roadmap (remaining work)

> **Status:** phases **C1–C4** and most of **C5** are implemented. This file now tracks only what is
> **not yet built**. For what exists, see the module docs: [README](../README.md) ·
> [Overview](overview.md) · [Architecture](architecture.md) · [Data Model](data-model.md) ·
> [Outbound & Templates](outbound-and-templates.md) · [Inbound & Linking](inbound-and-linking.md) ·
> [Implementation Status](implementation-status.md).
> 🇧🇷 [Versão em português](../pt-BR/product-plan.md)
>
> Related plans: [Agenda](../../agenda/en/product-plan.md) ·
> [Integrations](../../integrations/en/product-plan.md) · [Assistant](../../assistant/en/product-plan.md) ·
> [Messaging](../../../architecture/en/messaging.md)

---

## Design recap (already decided)

The boundary (Channels talks *to* the user), the principles (C1–C6), the internal Delivery/Ingress/
Addressing seam, the two-path template model, fan-out, inbound triage and routing are all documented in
the files linked above and are **built**. What remains is the phase-C5 operations tail.

---

## Phase C5 — Operations (remaining)

The module is fully usable without this; it lands piecemeal.

- **Quiet hours.** `chn005` gains `quiet_hours_start` / `quiet_hours_end` (in the user's zone) and a
  `quiet_hours_behaviour` of `suppress | deliver_anyway` — `defer_to_end` is dropped, because holding a
  delivery until morning is scheduling, and scheduling does not live here (C1). **Unblocked** — the
  user's IANA time zone is now available in Identity preferences — but not yet built.
- **Metrics.** Queue depth, dispatch latency, failure rate per channel, discarded updates. Waits on
  OpenTelemetry wiring in the Host — a cross-cutting task, not a Channels-only one.

## Maybe later *(not planned)*

- **Webhook driver.** Long polling covers ingress everywhere, including behind NAT, so a webhook only
  earns its place once the homelab is exposed over public HTTPS. The Tars client already supports it
  (`SetWebhookAsync`/`DeleteWebhookAsync`); the controller would hand incoming updates to the same
  triage the long-polling driver uses.
- **Manual retry of a dead row.** Re-queuing a dead notification from the UI. Not worth the surface
  while dead-letters are rare and already inspectable in the log.

## Related follow-ups (other modules)

- **Finances categories.** Its statement/import events are documented as planned but not published.
  Once they are, they get delivery categories for free — a small follow-up in Finances.

---

## Open questions

1. **Categories as a typed registry or a string.** Today `Category` is a string in the contract. A
   central registry would give startup validation at the cost of one more place to touch when a module
   is born. Leaning: string until it hurts.
2. **One address per channel.** Two Telegram chats (personal + a group) is out of scope; the unique
   constraint makes that a deliberate future change.
