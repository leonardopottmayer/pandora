# Integrations Module

> The credential store the rest of Pandora calls third parties with, inside the modular monolith.
> **Language:** English is the primary documentation. 🇧🇷 [Versão em português](pt-BR/README.md).

The **Integrations** module owns the credentials Pandora uses **on the user's behalf** to call
third-party services: the OAuth authorization dance, the tokens, their transparent refresh, and their
revocation — plus user-supplied API keys. Nothing else.

It is deliberately the smallest module in Pandora. It answers exactly one question for the rest of
the system:

> "Give me a valid credential for user *U* at provider *P*."

The guiding rule: **tokens never leave the module in storable form.** Consumers ask for a short-lived
access token (or an API key) through a port call — there is no `GetRefreshToken`, and refresh is
invisible to the caller. Refresh tokens are encrypted at rest with a key that lives outside the
database.

---

## How this documentation is organized

Start with the **Overview** for the boundary and vocabulary, then read the topic you need. Each file
carries both the *business context* (what it means and why) and the *technical rules* (aggregates,
schema, ports, endpoints).

| # | Document | What it covers |
|---|---|---|
| 1 | [Overview](en/overview.md) | What the module does, the Integrations/Channels boundary, principles, ubiquitous language, scope |
| 2 | [Architecture](en/architecture.md) | Project layout, domain building blocks, ports, key design decisions |
| 3 | [Data Model](en/data-model.md) | Schema catalog (`int001`, `int002`): columns, constraints, indexes |
| 4 | [OAuth & Credentials](en/oauth-and-credentials.md) | Authorization-code + PKCE flow, transparent refresh, encryption, API keys |
| 5 | [API Reference](en/api-reference.md) | Every endpoint under `/api/v{n}/integrations` |
| 6 | [Implementation Status](en/implementation-status.md) | What is built vs. planned |

The forward-looking roadmap (phases not yet implemented) lives in
[product-plan.md](en/product-plan.md).

---

## Quick facts

- **Backend:** `Pottmayer.Pandora.Modules.Integrations.*` (.NET 10, DDD, CQRS-style commands/queries).
- **Schema:** PostgreSQL schema `integrations`, tables prefixed `intXXX_`, PK `uuid_generate_v7()`.
- **Frontend:** a **Connected accounts** section under settings — `client-web/src/modules/integrations`.
- **API base:** `/api/v{version}/integrations`, authenticated (the callback is the sole anonymous endpoint).
- **Migrations:** `migrations/migrations/integrations/`.
- **Encryption:** `Pottmayer.Tars.Security.DataProtection` (`ISecretProtector`, AES-GCM, key outside the DB).
