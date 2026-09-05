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

- `access_type=offline` and `prompt=consent` on first connect, to actually receive a refresh token.
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

## 4. API keys (`auth_kind = api-key`)

For providers that authenticate with a static user-supplied key (OpenAI, Gemini), an account is stored
with `auth_kind = api-key`, the key held encrypted in `access_token_enc`, and no refresh token.
`GetApiKeyAsync(userId, provider)` decrypts and returns it; it fails with `NotAnApiKey` if the account
isn't an API-key account.

> **Status.** The read path (`GetApiKeyAsync`) is implemented. The endpoints to *register / rotate /
> remove* an API key, and the `openai`/`gemini` catalog entries, are phase I3 — see
> [product-plan.md](product-plan.md).

## 5. Revocation & disconnection

- **Disconnect** (`DELETE /accounts/{id}`) revokes the token at the provider (`RevokeAsync`) and then
  deletes the local account, publishing `ExternalAccountDisconnected`. A consumer (Agenda) is meant to
  disable the bindings that used it while **leaving the synced data in place** — disconnecting Google
  must not delete the user's events.
- **Provider-side revocation** (the user revokes access in their Google account page) surfaces as an
  `invalid_grant` on the next refresh → `status = revoked` + `ExternalAccountRevoked`.

Both contracts (`ExternalAccountRevoked`, `ExternalAccountDisconnected`) are **published today**. The
Channels subscriber that turns a revocation into a "reconnect needed" Telegram message is phase I2.
