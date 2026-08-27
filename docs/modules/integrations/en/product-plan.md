# Integrations Module — Roadmap (remaining work)

> **Status:** Phase **I1 (Core)** is implemented. This file now tracks only what is **not yet built**.
> For what exists, see the module docs: [README](../README.md) ·
> [Overview](overview.md) · [Architecture](architecture.md) · [Data Model](data-model.md) ·
> [OAuth & Credentials](oauth-and-credentials.md) · [Implementation Status](implementation-status.md).
> 🇧🇷 [Versão em português](../pt-BR/product-plan.md)
>
> Related plans: [Agenda](../../agenda/en/product-plan.md) ·
> [Channels](../../channels/en/product-plan.md) · [Assistant](../../assistant/en/product-plan.md)

---

## Design recap (already decided)

The boundary, principles (I1–I5), domain model, refresh semantics, encryption and authorization flow
are documented in the files linked above and are **built**. What remains is resilience, API-key
management, and more providers.

---

## Phase I2 — Resilience *(next)*

- **Channels reaction to revocation.** A subscriber to `ExternalAccountRevoked` (already published)
  that sends a Telegram message telling the user to reconnect, plus the template. Same for
  `ExternalAccountDisconnected` where a consumer needs to react.
- **`int003` integration event log** — append-only connects/refreshes/failures/revocations; the only
  way to answer "why did sync stop three days ago". Surface connection health in settings.
- **Done when:** revoking access in the Google account page produces a Telegram message telling the
  user to reconnect, and sync stops cleanly instead of retrying forever.

## Phase I3 — API keys *(prerequisite for [Assistant](../../assistant/en/product-plan.md) phase A5)*

- Register / rotate / remove endpoints for `auth_kind = api_key` accounts (the read path,
  `GetApiKeyAsync`, already exists).
- `openai` and `gemini` in the provider catalog, with **no authorization flow** — just a form with the
  key and a reachability test.
- **Done when:** Assistant can call OpenAI with a key it never saw in plaintext, and the same store
  holds the Google refresh token.

## Phase I4 — More providers *(driven by demand, not scheduled)*

- Microsoft (Outlook Calendar / To Do), CalDAV (generic; covers Apple/Fastmail/Nextcloud).

---

## Open questions

1. **In-process refresh gate vs. advisory lock.** The implementation serializes refresh with an
   in-process gate (single-process monolith). Revisit only if the host is scaled out.
2. **Multi-account per provider.** The unique constraint `(user_id, provider, provider_account_id)`
   already models two Google accounts. Whether a consumer's bindings can span them is the consumer's
   call, not this module's.
