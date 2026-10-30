# Implementation Status

[← Back to index](../README.md)

A snapshot of what is built in the codebase versus what is designed but not yet implemented. The
forward roadmap lives in [product-plan.md](product-plan.md).

---

## Implemented

| Area | Notes |
|---|---|
| **Module scaffold** | Seven layered projects; `integrations` schema; `int001`, `int002`, `int003`. |
| **Domain** | `ExternalAccount` + `OAuthState` aggregates; `AccountStatus`, `AuthKind` (`oauth`/`api-key`/`password`) value objects; `IntegrationErrors`. |
| **OAuth (Google)** | `GoogleOAuthProvider` (`BuildAuthorizationUrl` with `prompt=select_account consent` and an optional `login_hint`, `ExchangeCodeAsync`, `RefreshAsync`, `RevokeAsync`), resolved through `OAuthProviderRegistry`. |
| **Authorization flow** | `StartConnection` → consent URL with PKCE, optionally targeting an account; `HandleCallback` consumes the single-use `state`, exchanges the code, upserts by the returned `sub`, never overwrites a targeted account with another identity. |
| **Transparent refresh** | `GetAccessTokenAsync` refreshes near expiry, serialized per account by an **in-process gate**; `MarkRevoked` + `ExternalAccountRevoked` (with the account's label) on `invalid_grant`. |
| **Typed-in credentials (I3, I5)** | `CredentialProviderDescriptor` catalog (`gemini` api-key; `titan`, `imap` password). `SaveApiKey` (always a new account), `SavePassword` (upsert on username), `ReplaceSecret`, `RenameAccount`. |
| **Several accounts per provider (I5)** | `label` column; every port call by account id with an ownership check (`NotConnected` for missing or foreign); `IExternalAccountReader.GetAsync(userId, accountId)`; the catalog counts accounts per provider. |
| **Usage visibility (I5)** | `IExternalAccountUsageSource` (implemented today by Assistant, `assistant`/`chat`, and Communications, `communications`/`mail`); `GET /accounts` returns `usages`, skipping a failing source. |
| **Disconnect** | `DisconnectAccount` revokes at the provider, deletes locally, publishes `ExternalAccountDisconnected`. |
| **Contracts** | `ExternalAccountRevoked` (`Label` added, nullable), `ExternalAccountDisconnected`. Channels turns the first into the `integrations.account-revoked` notice ("Google · Personal"). |
| **Encryption** | Every credential column via Tars `ISecretProtector` (AES-GCM, key outside the DB). |
| **Event log (I2)** | `int003_integration_event_log` — append-only `connected`/`reconnected`/`refresh-failed`/`expired`/`revoked`/`disconnected`, each written in the same transaction as the state change it records. |
| **API** | `GET /providers`, `GET /accounts`, `GET /events`, `POST /{provider}/connect`, `GET /{provider}/callback`, `PUT /{provider}/api-key`, `PUT /{provider}/password`, `PUT /accounts/{id}/secret`, `PATCH /accounts/{id}`, `DELETE /accounts/{id}`. |
| **Frontend** | Connections screen grouped by provider (rename in place, add account, replace secret, reconnect, disconnect with the usages listed) + **Recent activity**; the shared `AccountSelect` picker (`modules/integrations/components`), used by the Assistant settings and the mail screens. |
| **Tests** | Domain (`ExternalAccountApiKeyTests`); integration `ApiKeyTests`, `AccountsTests`, `OAuthCallbackTests` (fake Google); front `accountSelection.test.ts`. |

### Notable deviations from the original plan

- **Refresh serialization** uses an **in-process gate**, not `pg_advisory_xact_lock` (single-process
  monolith).
- **No `reconnect` endpoint** — `connect` with an `accountId` reconnects or widens that account.
- **Successful refreshes are not logged** in `int003`: `int001.last_refreshed_at` already records the
  last success, so the log keeps the failure and lifecycle events and skips the hourly noise.
- **`password` upserts, `api-key` does not** (I5 said both create): a username is an identity, so the
  same mailbox saved twice is one account; a key has none.
- **The `titan` / `imap` descriptors** are registered here, with `gemini`, rather than by
  Communications — every typed-in provider lives in one catalog.

## Not yet implemented (designed / planned)

| Area | Status | Phase |
|---|---|---|
| **`openai` provider** | One `CredentialProviderDescriptor` away. | I3 |
| **More providers** (Microsoft, CalDAV) | Future, demand-driven. | I4 |

## Known open points

1. **In-process refresh gate vs. advisory lock** — fine for the single-process monolith; revisit if the
   host is ever scaled out.
