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

## Phase I2 — Resilience ✅ *(done)*

Both halves shipped. See [implementation-status.md](implementation-status.md).

- **Channels reaction to revocation.** Channels subscribes to `ExternalAccountRevoked` and sends the
  `integrations.account-revoked` "reconnect" notification, fanned out to the user's channels.
- **`int003` integration event log** — append-only lifecycle + failure log, each row written in the
  same transaction as the change it records; surfaced as a "Recent activity" timeline in settings.
  Successful refreshes are intentionally not logged (`int001.last_refreshed_at` covers the last one).

## Phase I3 — API keys *(prerequisite for [Assistant](../../assistant/en/product-plan.md) phase A5)*

- Register / rotate / remove endpoints for `auth_kind = api-key` accounts (the read path,
  `GetApiKeyAsync`, already exists).
- `openai` and `gemini` in the provider catalog, with **no authorization flow** — just a form with the
  key and a reachability test.
- **Done when:** Assistant can call OpenAI with a key it never saw in plaintext, and the same store
  holds the Google refresh token.

## Phase I5 — Multiple accounts ✅ *(built; real-Google check pending)*

Any number of accounts per provider, each named by the user, and each use binds to the account it runs
on (no default account). Built end to end — port by account id, `password` auth kind, `label`,
`IExternalAccountUsageSource`, the OAuth add/reconnect flows, the connections screen and the shared
`AccountSelect` — see [oauth-and-credentials §6](oauth-and-credentials.md#6-several-accounts-per-provider),
[api-reference](api-reference.md) and [implementation-status](implementation-status.md).

**Still to check by hand** (needs the Google Cloud project): three Google accounts and two Gemini keys
connected and named; the Assistant runs on the key picked in its settings; disconnecting a key in use
shows "Used by: Assistant · chat" and still disconnects; the Assistant then asks for an account.

## Phase I4 — More providers *(driven by demand, not scheduled)*

- Microsoft (Outlook Calendar / To Do), CalDAV (generic; covers Apple/Fastmail/Nextcloud).

---

## Open questions

1. **In-process refresh gate vs. advisory lock.** The implementation serializes refresh with an
   in-process gate (single-process monolith). Revisit only if the host is scaled out.
2. ~~**Multi-account per provider.**~~ Decided in phase I5 above: explicit binding by
   account id, no default account.
