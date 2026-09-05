# Channels Module — Roadmap (remaining work)

> **Status:** phases **C1–C5** are implemented. This file now tracks only what is
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
the files linked above and are **built**. Phase C5 is now complete too.

---

## Phase C5 — Operations *(done)*

- **Quiet hours — built.** A global daily "do not disturb" window (`chn007_user_notification_setting`),
  `suppress | deliver-anyway` (`defer_to_end` dropped — holding until morning is scheduling, which does
  not live here, C1). Evaluated in the user's own IANA zone (resolved from Identity preferences at
  delivery time) and applied in `NotifyUserRequestedHandler` before fan-out. It landed as a **global
  per-user** setting rather than columns on `chn005`, because a single "do not disturb" on a
  per-category table would have meant a row per category. Security notifications bypass it.
- **Metrics — built.** A `ChannelsMetrics` meter (`Pottmayer.Pandora.Modules.Channels`) exposes queue
  depth, dispatch duration, dispatched-count by channel/outcome, and discarded inbound updates,
  subscribed by a `Pottmayer.Pandora.*` `AddMeter` wildcard in the shared observability wiring and
  exported over OTLP (the Host's OpenTelemetry pipeline, which landed since this was first planned).

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
