# Implementation Status

[← Back to index](../README.md)

A snapshot of what is built in the codebase versus what is designed but not yet implemented. The
forward roadmap lives in [product-plan.md](product-plan.md).

---

## Implemented (phase I1 — Core)

| Area | Notes |
|---|---|
| **Module scaffold** | Seven layered projects; `integrations` schema; `int001`, `int002`. |
| **Domain** | `ExternalAccount` + `OAuthState` aggregates; `AccountStatus`, `AuthKind` value objects; `IntegrationErrors`. |
| **OAuth (Google)** | `GoogleOAuthProvider` (`BuildAuthorizationUrl`, `ExchangeCodeAsync`, `RefreshAsync`, `RevokeAsync`), resolved through `OAuthProviderRegistry`. |
| **Authorization flow** | `StartConnection` → consent URL with PKCE; `HandleCallback` consumes single-use `state`, exchanges the code, upserts the encrypted account. |
| **Transparent refresh** | `ExternalCredentialProvider.GetAccessTokenAsync` refreshes near expiry, serialized per account by an **in-process gate**; `MarkRevoked` + `ExternalAccountRevoked` on `invalid_grant`. |
| **API-key read path** | `GetApiKeyAsync` decrypts and returns a stored `api_key` account's key. |
| **Disconnect** | `DisconnectAccount` revokes at the provider, deletes locally, publishes `ExternalAccountDisconnected`. |
| **Ports** | `IExternalCredentialProvider`, `IExternalAccountReader`, `ExternalAccessToken`, `ExternalAccountSummary` in `Abstractions`. |
| **Contracts** | `ExternalAccountRevoked`, `ExternalAccountDisconnected` published. |
| **Encryption** | Every credential column via Tars `ISecretProtector` (AES-GCM, key outside the DB). |
| **API** | `GET /providers`, `GET /accounts`, `POST /{provider}/connect`, `GET /{provider}/callback`, `DELETE /accounts/{id}`. |
| **Frontend** | Connected-accounts settings section in `client-web/src/modules/integrations`. |

### Notable deviations from the original plan

- **Refresh serialization** uses an **in-process gate**, not `pg_advisory_xact_lock` (single-process
  monolith).
- **No `reconnect` endpoint** — re-running `connect` re-consents and widens scopes.
- The contracts (`ExternalAccountRevoked`/`ExternalAccountDisconnected`) already exist. Channels now
  consumes `ExternalAccountRevoked` (`ExternalAccountRevokedHandler` → the `integrations.account-revoked`
  template, fanned out to the user's channels) — the first half of I2. `ExternalAccountDisconnected`
  has no notifier: a disconnect is the user's own action, so consumers just disable their bindings.

## Not yet implemented (designed / planned)

| Area | Status | Phase |
|---|---|---|
| **`int003` integration event log** | Designed, table not created. | I2 |
| **API-key management endpoints** | `GetApiKeyAsync` exists, but there is no endpoint to register/rotate/remove an API key, so no `api_key` account can be created yet. | I3 |
| **`openai` / `gemini` providers** | Not in the catalog; no key form or reachability test. | I3 |
| **More providers** (Microsoft, CalDAV) | Future, demand-driven. | I4 |

## Known open points

1. **In-process refresh gate vs. advisory lock** — fine for the single-process monolith; revisit if the
   host is ever scaled out.
2. **Multi-account per provider** — the unique constraint already allows two Google accounts; whether a
   consumer's bindings can span them is the consumer's call.
