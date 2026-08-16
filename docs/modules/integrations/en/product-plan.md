# Integrations Module — Product Plan

> **Status:** Plan. Nothing in this document is implemented yet.
> 🇧🇷 [Versão em português](../pt-BR/product-plan.md)
>
> Related plans: [Agenda](../../agenda/en/product-plan.md) ·
> [Channels](../../channels/en/product-plan.md) ·
> [Assistant](../../assistant/en/product-plan.md)

---

## 1. What the module does

**Integrations** owns the credentials Pandora uses **on the user's behalf** to call third-party
services: the OAuth dance, the tokens, their refresh, and their revocation — plus the API keys the
user supplies themselves. Nothing else.

It is deliberately the smallest module in Pandora. It answers exactly one question for the rest of
the system:

> "Give me a valid credential for user *U* at provider *P*."

[Agenda](../../agenda/en/product-plan.md) is its first and, at launch, only consumer — it needs a
Google token to sync calendars and tasks. But credentials for external services are not a calendar
concern, and Finances (open finance) and Assistant (hosted LLM keys) are both plausible second
consumers. Keeping the credential store separate from day one means neither of them has to reach
into Agenda's schema later.

### What it is not

It is **not** a sync engine. It does not know what a calendar, an event or a task is. The sync
cursors, entity mappings, conflict resolution and provider adapters all live in Agenda, which is the
module that understands the data being synced. Integrations hands out tokens; Agenda uses them.

It is also **not** the owner of a *channel*. A Telegram chat id is an **address** where Pandora
reaches the user, and the bot token is a **deployment** credential, not the user's — neither is "the
user's credential for Pandora to call a third party as them". Both live in
[Channels](../../channels/en/product-plan.md). The boundary, in one line:

> **Integrations:** Pandora calls a third party *as* the user. **Channels:** Pandora talks *to* the
> user.

None of this shows in the interface: settings has one **Connections** section, composed of two
backend sections. UI unity is not module unity.

---

## 2. Naming and coordinates

| Thing | Value |
|---|---|
| Backend projects | `Pottmayer.Pandora.Modules.Integrations.{Abstractions,Application,Contracts,Domain,Infrastructure,Persistence,Presentation}` |
| PostgreSQL schema | `integrations` |
| Table prefix | `intXXX_`, PK `uuid_generate_v7()` |
| API base | `/api/v{version}/integrations` |
| Frontend | `client-web/src/modules/integrations` (or a settings section — see §7) |
| Migrations | `migrations/migrations/integrations/` |

---

## 3. Principles

1. **Tokens never leave the module in storable form.** Consumers receive a short-lived access token
   through a port call, not a repository. There is no `GetRefreshToken`. *(I1)*
2. **Refresh is invisible.** `GetAccessTokenAsync` returns something valid or fails. The caller never
   implements refresh, and there is exactly one place that can race on it. *(I2)*
3. **Encrypted at rest, always.** Refresh tokens are the crown jewels of the whole system — a stolen
   one reads the user's real calendar and mailbox forever. They are encrypted with a key that is not
   in the database. *(I3)*
4. **Providers are configuration plus an adapter.** Adding Microsoft is a client-id, a metadata
   entry and an `IOAuthProvider` implementation. The domain does not change. *(I4)*
5. **A revoked connection degrades, never crashes.** A consumer asking for a token from a revoked
   account gets a typed failure and a notification is sent to the user, not a stack trace in a
   background job. *(I5)*

---

## 4. Domain model

### 4.1 Schema catalog

**`int001_external_account`** — one connected third-party account.

| Column | Notes |
|---|---|
| `user_id` | Owner. |
| `provider` | `google` today; `microsoft`, `apple`, `caldav`, `openai`, `gemini` later. |
| `auth_kind` | `oauth` \| `api_key`. Decides which columns are required and whether there is an authorization flow. |
| `provider_account_id` | The provider's stable subject id. Unique with `(user_id, provider)`. For `api_key`, a user-chosen discriminator (the key's label). |
| `display_name` | The account's email/handle, shown in settings. |
| `scopes` | Granted scopes as stored — used to detect that a new feature needs re-consent. |
| `access_token_enc`, `access_token_expires_at` | Encrypted; short-lived. |
| `refresh_token_enc` | Encrypted. Null when the provider issues none. |
| `status` | `connected` \| `expired` \| `revoked` \| `needs_consent`. |
| `connected_at`, `last_refreshed_at`, `last_error` | |

**`int002_oauth_state`** — the in-flight authorization request.

| Column | Notes |
|---|---|
| `user_id`, `provider`, `state` | `state` is the CSRF token, unique, single-use. |
| `code_verifier_enc` | PKCE. Encrypted, because it is a credential for the duration of the flow. |
| `redirect_after` | Where to bounce the browser back to in the SPA. |
| `expires_at`, `consumed_at` | 10-minute TTL, single use. |

**`int003_integration_event_log`** *(optional, phase I3)* — append-only record of connects,
refreshes, failures and revocations. Small, cheap, and the only way to answer "why did sync stop
three days ago".

### 4.2 Ports

Published from `Integrations.Abstractions`, the only thing other modules reference:

```csharp
public interface IExternalCredentialProvider
{
    // auth_kind = oauth — refreshes invisibly
    Task<Result<ExternalAccessToken>> GetAccessTokenAsync(
        Guid userId, string provider, CancellationToken ct = default);

    // auth_kind = api_key — decrypts and returns; no expiry, no refresh
    Task<Result<string>> GetApiKeyAsync(
        Guid userId, string provider, CancellationToken ct = default);
}

public interface IExternalAccountReader
{
    Task<IReadOnlyList<ExternalAccountSummary>> ListAsync(Guid userId, CancellationToken ct = default);
    Task<ExternalAccountSummary?> GetAsync(Guid externalAccountId, CancellationToken ct = default);
}
```

`ExternalAccessToken` carries the token string, its expiry and the granted scopes. It is a transient
value — never persisted by the consumer, never logged.

Internal to the module:

```csharp
public interface IOAuthProvider          // one per provider
{
    string Name { get; }
    Uri BuildAuthorizationUrl(OAuthAuthorizationRequest request);
    Task<OAuthTokens> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct);
    Task<OAuthTokens> RefreshAsync(string refreshToken, CancellationToken ct);
    Task RevokeAsync(string token, CancellationToken ct);
}

```

From Tars (`Pottmayer.Tars.Security.DataProtection`):

```csharp
public interface ISecretProtector          // AES-GCM over a key from configuration/secret store
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
```

### 4.3 Refresh semantics

`GetAccessTokenAsync` is the hot path — Agenda's sync job calls it every few minutes:

1. If the cached access token has more than a 60-second margin left, return it.
2. Otherwise take a per-account advisory lock (`pg_advisory_xact_lock` on the account id), re-read,
   and refresh only if still needed. Two concurrent sync jobs must not burn two refreshes; some
   providers invalidate the previous refresh token on rotation.
3. Persist the new pair, encrypted; update `last_refreshed_at`.
4. On `invalid_grant`, mark the account `revoked`, publish `ExternalAccountRevoked`, and return a
   typed failure.

`ExternalAccountRevoked` is consumed by Channels, which tells the user their Google connection
needs reconnecting — the one case where a background failure must reach a human.

### 4.4 Key management

`ISecretProtector` reads a 256-bit key from configuration (environment variable in Docker, mounted
secret in the homelab), never from the database. Ciphertext is stored with a key-version prefix so
rotation is a background re-encrypt rather than a reconnect-everything event. The key living outside
the database is the entire point: a database dump alone must not yield working Google credentials.

---

## 5. Authorization flow

Server-side authorization-code flow with PKCE. The SPA never sees a token.

```
1. SPA        → POST /integrations/google/connect
                ← { authorizationUrl }              (state + verifier persisted)
2. Browser    → provider consent screen
3. Provider   → GET /integrations/google/callback?code=&state=
4. Backend    → validate & consume state, exchange code, upsert account (encrypted)
                ← 302 to redirect_after in the SPA
5. SPA        → GET /integrations/accounts        ← shows the connection as live
```

The callback is the only anonymous endpoint; it authenticates by the single-use `state` it issued
itself. Scopes are requested **incrementally** — connecting for calendar asks only for calendar
scopes; enabling task sync later triggers a re-consent that widens them and updates `scopes`.

### Google specifics
- `access_type=offline` and `prompt=consent` on first connect, to actually receive a refresh token.
- Scopes: `calendar` and `calendar.events` for Agenda phase 5; `tasks` added in phase 6.
- A Google Cloud project with OAuth consent configured is a deployment prerequisite, documented
  under `docs/deployment/`.

---

## 6. API surface

```
GET    /integrations/providers                 → catalog: name, description, scopes, connected?
POST   /integrations/{provider}/connect        → { authorizationUrl }
GET    /integrations/{provider}/callback       → anonymous; consumes state, redirects to the SPA
GET    /integrations/accounts                  → connected accounts, status, scopes, last error
POST   /integrations/accounts/{id}/reconnect   → re-consent for widened scopes
DELETE /integrations/accounts/{id}             → revoke at the provider, then delete locally
```

Deleting an account publishes `ExternalAccountDisconnected`. Agenda subscribes and disables the
bindings that used it, leaving the synced data in place — disconnecting Google must not delete the
user's events.

---

## 7. Frontend

This module has no screen of its own. It contributes a **Connected accounts** section to the
settings area: provider cards with status, scopes, connect/disconnect, and the "reconnect needed"
banner when `status = revoked`. Agenda's own settings screen links here.

If the section grows past a handful of providers it becomes `client-web/src/modules/integrations`;
until then it lives with the rest of settings, and the module's frontend footprint is a hook plus a
component.

---

## 8. Roadmap

### Phase I1 — Core *(prerequisite for Agenda phase 5)*
- Seven projects, `integrations` schema, `int001`/`int002`.
- `ISecretProtector` (AES-GCM, versioned key), `IOAuthProvider`, Google implementation.
- Connect/callback/list/disconnect endpoints; `IExternalCredentialProvider` with locked refresh.
- Settings UI section.
- **Done when:** Agenda's sync job obtains a valid Google token across an access-token expiry
  without any code in Agenda knowing what a refresh token is.

### Phase I2 — Resilience
- `ExternalAccountRevoked` / `ExternalAccountDisconnected` contracts and Channels templates.
- Incremental scope widening and the re-consent path.
- `int003` event log; connection health surfaced in settings.
- **Done when:** revoking access in the Google account page produces a Telegram message telling the
  user to reconnect, and sync stops cleanly instead of retrying forever.

### Phase I3 — API keys *(prerequisite for Assistant phase A5)*
- `auth_kind = api_key` on `int001`; register/rotate/remove endpoints; `GetApiKeyAsync`.
- `openai` and `gemini` providers in the catalogue, with no authorization flow — just a form with the
  key and a reachability test.
- **Done when:** [Assistant](../../assistant/en/product-plan.md) can call OpenAI with a key it never
  saw in plaintext, and the same store holds the Google refresh token.

### Phase I4 — More providers *(driven by demand, not scheduled)*
- Microsoft (Outlook Calendar / To Do), CalDAV (generic, covers Apple/Fastmail/Nextcloud).

---

## 9. Open questions

1. ~~**Does the module hold non-OAuth secrets?**~~ **Decided: yes.** `int001` gains
   `auth_kind = oauth | api_key`, and an OpenAI key is a row like any other — same store, same
   encryption, without the authorization flow. Closes
   [Assistant open question 1](../../assistant/en/product-plan.md#9-open-questions).
2. ~~**Where `ISecretProtector` lives.**~~ **Decided:** in Tars,
   `Pottmayer.Tars.Security.DataProtection`. It stopped being single-consumer code — this module uses
   it for OAuth and for API keys, and roberto has the same problem.
3. **Multi-account per provider.** The unique constraint is `(user_id, provider, provider_account_id)`,
   so two Google accounts are already modelled. Whether Agenda's bindings can span them is Agenda's
   call, not this module's.
