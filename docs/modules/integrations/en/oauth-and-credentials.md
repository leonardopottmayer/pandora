# OAuth & Credentials

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Data Model](data-model.md)

---

## 1. Authorization flow

Server-side authorization-code flow with **PKCE**. The SPA never sees a token.

```
1. SPA        → POST /integrations/google/connect { redirectAfter, scopes? }
                ← { authorizationUrl }               (int002 state + encrypted verifier persisted)
2. Browser    → provider consent screen
3. Provider   → GET /integrations/google/callback?code=&state=
4. Backend    → validate & consume state (single-use), exchange code with the verifier,
                upsert int001 (tokens encrypted), 302 to redirect_after in the SPA
5. SPA        → GET /integrations/accounts          ← shows the connection as live
```

The callback (`GET /{provider}/callback`) is the only **anonymous** endpoint; it authenticates by the
single-use `state` it issued itself. A missing/blank `code` or `state`, or a state already consumed
or expired, redirects home with an error outcome rather than throwing.

**Re-consent / widened scopes.** Running `connect` again for an already-connected provider re-runs the
consent and updates the stored `scopes` — this is how a later feature (e.g. enabling task sync)
widens permissions. There is no separate `reconnect` endpoint; `connect` covers it.

### Google specifics

- `access_type=offline` and `prompt=select_account consent`: a refresh token on every consent, and
  Google's account chooser, so a user with several Google accounts picks which one to connect.
  A reconnect of a known account also sends `login_hint=<its email>`.
- Scopes: calendar scopes for Agenda calendar sync; task scopes added when task sync is enabled.
- A Google Cloud project with the OAuth consent screen configured is a **deployment prerequisite**
  (client id/secret supplied via `GoogleOAuthOptions`), documented under `docs/deployment/`.

## 2. Transparent refresh

`GetAccessTokenAsync` is the hot path — a sync job calls it every few minutes:

1. If the cached access token still has margin before expiry, return it.
2. Otherwise **serialize the refresh per account with an in-process gate** (not a Postgres advisory
   lock), re-read, and refresh only if still needed. Two concurrent callers must not burn two
   refreshes — some providers invalidate the previous refresh token on rotation.
3. Persist the new pair, encrypted; update `last_refreshed_at`.
4. On `invalid_grant`, call `MarkRevoked`, publish `ExternalAccountRevoked`, and return a typed
   failure (`IntegrationErrors.AccountRevoked`) — never an exception into the caller's job.

> **Design note.** The plan originally called for a `pg_advisory_xact_lock`. The implementation uses an
> in-process gate because the monolith runs as a single process; revisit if the host is scaled out.

## 3. Encryption at rest

Every credential column (`access_token_enc`, `refresh_token_enc`, `code_verifier_enc`) is written
through `ISecretProtector` (Tars `Security.DataProtection`, AES-GCM). The key is read from
configuration — an environment variable in Docker, a mounted secret in the homelab — **never from the
database**. That separation is the entire point: a database dump alone must not yield working
credentials.

## 4. Typed-in credentials (`api-key`, `password`)

For providers that authenticate with something the user types in, there is no authorization flow —
only a catalog entry (`CredentialProviderDescriptor`: name, display name, auth kind):

- **API keys** (`gemini` today; OpenAI is one registration more). `PUT /{provider}/api-key` always
  creates a **new** account — a key has no identity at the provider, so the row's own id is its
  `provider_account_id`, and two keys never collide. `GetApiKeyAsync(userId, accountId)` decrypts it.
- **Username + password** (`titan`, `imap`). `PUT /{provider}/password` upserts on the username: the
  same mailbox again replaces its password, another username is another account.
  `GetPasswordAsync(userId, accountId)` returns `PasswordCredential(Username, Password)`.

Both keep the secret encrypted in `access_token_enc`, with no expiry and no refresh token.
`PUT /accounts/{id}/secret` rotates it; an OAuth account is renewed by reconnecting instead.

## 6. Several accounts per provider

A user can hold any number of accounts per provider, and **each use picks the account it runs on**:
the consumer stores the account id (a mailbox, a calendar binding, the Assistant's
`chat_account_id`) and asks the port by that id. There is no default account and no "first one"
fallback. Connecting an account grants nothing by itself.

- **Add another Google account:** `connect` with no target; the callback upserts by the returned
  `sub` — a new `sub` creates a row, a known one reconnects it.
- **Reconnect or widen one account:** `connect` with `accountId`; `int002.target_account_id` remembers
  it. If the user signs in with a different identity, the target is **not** overwritten: the account
  that came back is added/reconnected and the SPA lands with `?integration=other-account`.
- **Used by:** each consumer module implements `IExternalAccountUsageSource`; `GET /accounts` returns
  `usages` per account, and disconnecting an account in use warns (without blocking).
- **A dangling binding** (the account was disconnected) surfaces at the consumer's next credential
  call as `NotConnected`; consumers that must react at once subscribe to the events (§5).

## 5. Revocation & disconnection

- **Disconnect** (`DELETE /accounts/{id}`) revokes the token at the provider (`RevokeAsync`) and then
  deletes the local account, publishing `ExternalAccountDisconnected`. A consumer (Agenda) is meant to
  disable the bindings that used it while **leaving the synced data in place** — disconnecting Google
  must not delete the user's events.
- **Provider-side revocation** (the user revokes access in their Google account page) surfaces as an
  `invalid_grant` on the next refresh → `status = revoked` + `ExternalAccountRevoked`.

Both contracts (`ExternalAccountRevoked`, `ExternalAccountDisconnected`) are **published today**. The
Channels subscriber that turns a revocation into a "reconnect needed" Telegram message is phase I2.
