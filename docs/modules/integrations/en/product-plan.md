# Integrations Module — Roadmap (remaining work)

> **Status:** phases **I1 (core)**, **I2 (resilience)**, **I3 (typed-in credentials)** and **I5
> (several accounts per provider)** are implemented. This file tracks only what is **not yet built**.
> For what exists, see the module docs: [README](../README.md) ·
> [Overview](overview.md) · [Architecture](architecture.md) · [Data Model](data-model.md) ·
> [OAuth & Credentials](oauth-and-credentials.md) · [Implementation Status](implementation-status.md).
> 🇧🇷 [Versão em português](../pt-BR/product-plan.md)
>
> Related plans: [Agenda](../../agenda/en/product-plan.md) ·
> [Channels](../../channels/en/product-plan.md) · [Assistant](../../assistant/en/product-plan.md)

---

## Check I5 against real Google

I5 is built and covered by tests, but it has not been checked by hand against the real Google Cloud
project:

- three Google accounts and two Gemini keys connected and named;
- the Assistant runs on the key picked in its settings;
- disconnecting a key in use shows "Used by: Assistant · chat" and still disconnects;
- the Assistant then asks for an account.

## `openai` provider

One `CredentialProviderDescriptor(..., AuthKind.ApiKey)` in the catalog. It lands with the Assistant's
[second provider](../../assistant/en/product-plan.md#1-second-provider-openai).

## Phase I4 — More providers *(driven by demand, not scheduled)*

- Microsoft (Outlook Calendar / To Do), CalDAV (generic; covers Apple/Fastmail/Nextcloud).

---

## Open questions

1. **In-process refresh gate vs. advisory lock.** The implementation serializes refresh with an
   in-process gate (single-process monolith). Revisit only if the host is scaled out.
