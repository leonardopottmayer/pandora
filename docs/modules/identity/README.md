# Identity Module

> Authentication, account lifecycle, MFA and user preferences inside the Pandora modular monolith.
> **Language:** English is the primary documentation. 🇧🇷 [Versão em português](pt-BR/README.md).

The **Identity** module owns *who the user is*: sign-up and account activation, sign-in with JWT
access tokens and rotating refresh tokens, password reset and change, TOTP-based multi-factor
authentication with recovery codes, and per-user preferences (theme, language, time zone, week start,
default alert offset).

The guiding rule: **Identity issues and validates tokens; it never stores a reusable secret in the
clear.** Passwords are hashed with **Argon2id**, refresh tokens and every one-time token are stored as
**hashes**, and the MFA secret is **encrypted at rest**. The JWT plumbing itself lives in Tars
(`Pottmayer.Tars.Security.Identity`); Identity owns the users, the flows and the persistence.

---

## How this documentation is organized

Start with the **Overview** for the vocabulary and scope, then read the topic you need.

| # | Document | What it covers |
|---|---|---|
| 1 | [Overview](en/overview.md) | What the module does, principles, ubiquitous language, scope |
| 2 | [Architecture](en/architecture.md) | Project layout, the User aggregate & entities, ports/services, decisions |
| 3 | [Data Model](en/data-model.md) | Schema catalog (`idt001`–`idt008`): columns, constraints, indexes |
| 4 | [Authentication](en/authentication.md) | Sign-up, activation, sign-in, JWT + refresh rotation, sign-out, password reset/change |
| 5 | [MFA](en/mfa.md) | TOTP setup/enable/disable, recovery codes, the sign-in challenge |
| 6 | [Preferences](en/preferences.md) | Theme, language, time zone, week start, default alert offset |
| 7 | [API Reference](en/api-reference.md) | Every endpoint under `/api/v{n}/identity` |
| 8 | [Implementation Status](en/implementation-status.md) | What is built vs. planned |

---

## Quick facts

- **Backend:** `Pottmayer.Pandora.Modules.Identity.*` (.NET 10, DDD, CQRS-style commands/queries).
- **Schema:** PostgreSQL schema `identity`, tables prefixed `idtXXX_`, PK `uuid_generate_v7()`.
- **Frontend:** `client-web/src/modules/identity` (sign-in, sign-up, MFA, preferences).
- **API base:** `/api/v{version}/identity`, with anonymous auth endpoints and authenticated
  account/preferences endpoints.
- **Migrations:** `migrations/migrations/identity/`.
- **Tars building blocks:** `Pottmayer.Tars.Security.Identity` (JWT issue/validate + refresh token
  service), `Pottmayer.Tars.Security.DataProtection` (`ISecretProtector`).
