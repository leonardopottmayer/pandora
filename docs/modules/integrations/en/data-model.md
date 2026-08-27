# Data Model

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [OAuth & Credentials](oauth-and-credentials.md)

PostgreSQL schema **`integrations`**. Conventions: PK `uuid DEFAULT uuid_generate_v7()`, `TIMESTAMPTZ`
for timestamps, named constraints (`pk_intXXX`, `uq_intXXX_*`, `chk_intXXX_*`), enums stored as
`VARCHAR` + `CHECK`. Every user-owned table has `user_id NOT NULL`. Every credential column is stored
**encrypted** (`*_enc`) via `ISecretProtector`.

Migrations live in `migrations/migrations/integrations/`.

## Table catalog

| # | Table | Contents |
|---|---|---|
| int001 | `external_account` | One connected third-party account + its encrypted credentials |
| int002 | `oauth_state` | One in-flight authorization request (CSRF state + PKCE verifier) |
| int003 | *(reserved)* | Integration event log — **not yet implemented** (phase I2) |

---

## int001_external_account

One connected third-party account. Holds the credentials Pandora uses on the user's behalf,
encrypted at rest with a key that lives outside the database.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | owner |
| `provider` | varchar(40) NOT NULL | `google` today; `microsoft`, `openai`, `gemini`, … later |
| `auth_kind` | varchar(20) NOT NULL | `oauth \| api_key` |
| `provider_account_id` | varchar(255) NOT NULL | provider's stable subject id; for `api_key`, a user-chosen label |
| `display_name` | varchar(255) NULL | the account's email/handle, shown in settings |
| `scopes` | text NOT NULL DEFAULT '' | granted scopes as stored; used to detect a needed re-consent |
| `access_token_enc` | text NULL | encrypted; short-lived (also holds the API key for `api_key`) |
| `access_token_expires_at` | timestamptz NULL | |
| `refresh_token_enc` | text NULL | encrypted; null when the provider issues none |
| `status` | varchar(20) NOT NULL | `connected \| expired \| revoked \| needs_consent` |
| `connected_at` | timestamptz NOT NULL | |
| `last_refreshed_at` | timestamptz NULL | |
| `last_error` | text NULL | last refresh/revocation error, surfaced in settings |
| `created_by/created_at/updated_by/updated_at` | | audit columns |

Constraints: `pk_int001`, `chk_int001_auth_kind (oauth|api_key)`,
`chk_int001_status (connected|expired|revoked|needs_consent)`,
`uq_int001_user_provider_account (user_id, provider, provider_account_id)` — one account per
(user, provider, account), so two Google accounts are already modelled by the discriminating
`provider_account_id`.

## int002_oauth_state

One in-flight authorization request. The callback authenticates by consuming exactly the state it
issued: single use, short lived. The PKCE verifier is encrypted for the duration of the flow.

| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `user_id` | uuid NOT NULL | who started the flow |
| `provider` | varchar(40) NOT NULL | |
| `state` | varchar(255) NOT NULL | the CSRF token — **unique**, single-use |
| `code_verifier_enc` | text NOT NULL | PKCE verifier, encrypted |
| `redirect_after` | varchar(500) NOT NULL | where to bounce the browser back to in the SPA |
| `expires_at` | timestamptz NOT NULL | ~10-minute TTL |
| `consumed_at` | timestamptz NULL | set on first use; a second callback with the same state fails |

Constraints: `pk_int002`, `uq_int002_state (state)` — a callback resolves to exactly one request.

## int003_integration_event_log *(reserved — not implemented)*

Planned for phase I2: an append-only record of connects, refreshes, failures and revocations — the
way to answer "why did sync stop three days ago". See [product-plan.md](product-plan.md).
