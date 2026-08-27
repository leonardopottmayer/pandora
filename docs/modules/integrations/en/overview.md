# Overview — Boundary & Principles

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Data Model](data-model.md)

---

## 1. What the module does

**Integrations** owns the credentials Pandora uses **on the user's behalf** to call third-party
services. It handles:

- The **OAuth authorization-code flow with PKCE**: building the consent URL, persisting the in-flight
  request, and consuming the callback.
- **Token storage**, encrypted at rest — access tokens, refresh tokens, granted scopes.
- **Transparent refresh**: a consumer asking for an access token gets a valid one or a typed failure;
  it never sees or implements refresh.
- **Revocation and disconnection**: revoking at the provider and deleting the local account.
- **User-supplied API keys** (`auth_kind = api_key`) as an alternative to OAuth, for providers that
  authenticate with a static key.

It is deliberately the smallest module in Pandora and answers exactly one question:

> "Give me a valid credential for user *U* at provider *P*."

[Agenda](../../agenda/en/product-plan.md) is its first consumer — it needs a Google token to sync
calendars and tasks. Assistant (hosted LLM keys) and Finances (open finance) are plausible second
consumers, which is why the credential store is separate from day one: neither has to reach into
another module's schema later.

## 2. What it is not

- **Not a sync engine.** It does not know what a calendar, an event or a task is. Sync cursors, entity
  mappings and conflict resolution live in the consuming module (Agenda). Integrations hands out
  tokens; Agenda uses them.
- **Not the owner of a *channel*.** A Telegram chat id is an **address** where Pandora reaches the
  user, and a bot token is a **deployment** credential — both live in
  [Channels](../../channels/en/product-plan.md). The boundary in one line:

> **Integrations:** Pandora calls a third party *as* the user. **Channels:** Pandora talks *to* the user.

None of this shows in the UI: settings has one **Connected accounts** section, composed of two
backend concerns. UI unity is not module unity.

## 3. Core principles

1. **Tokens never leave the module in storable form.** Consumers receive a short-lived access token
   through a port call, not a repository. There is no `GetRefreshToken`. *(Design decision I1.)*
2. **Refresh is invisible.** `GetAccessTokenAsync` returns something valid or fails. The caller never
   implements refresh, and there is exactly one place that can race on it. *(I2.)*
3. **Encrypted at rest, always.** Refresh tokens are the crown jewels: a stolen one reads the user's
   real calendar and mailbox indefinitely. They are encrypted with a key that is **not** in the
   database. *(I3.)*
4. **Providers are configuration plus an adapter.** Adding Microsoft is a client-id, a catalog entry
   and an `IOAuthProvider` implementation. The domain does not change. *(I4.)*
5. **A revoked connection degrades, never crashes.** A consumer asking for a token from a revoked
   account gets a typed failure (and, once wired, a user notification), not a stack trace in a
   background job. *(I5.)*

## 4. Ubiquitous language (glossary)

| Term | Meaning |
|---|---|
| **External account** | One connected third-party account (`int001`). Holds the encrypted credentials Pandora uses on the user's behalf. Identified by `(user_id, provider, provider_account_id)`. |
| **Provider** | A third-party service Pandora connects to: `google` today; `microsoft`, `openai`, `gemini` and others later. |
| **Auth kind** | How an account authenticates: `oauth` (refreshable tokens) or `api_key` (a static user-supplied key). |
| **OAuth state** | An in-flight authorization request (`int002`). The `state` is the single-use CSRF token; the PKCE `code_verifier` is stored encrypted for the duration of the flow. |
| **Access token** | A short-lived OAuth credential, returned to consumers as a transient `ExternalAccessToken` (token + expiry + scopes). Never persisted by the consumer. |
| **Refresh token** | A long-lived OAuth credential used only inside the module to mint new access tokens. Encrypted at rest, never handed out. |
| **Scopes** | The granted permissions, stored to detect that a new feature needs re-consent. |
| **Secret protector** | The Tars `ISecretProtector` (AES-GCM) that encrypts/decrypts every credential column, with a key sourced outside the database. |

## 5. Scope

### In scope (implemented — see [Implementation Status](implementation-status.md))

The `integrations` schema (`int001`, `int002`); the Google OAuth provider; the full
connect → consent → callback → store cycle with PKCE; transparent, serialized token refresh;
disconnect with provider-side revocation; the `IExternalCredentialProvider` /
`IExternalAccountReader` ports; the providers/accounts read endpoints; and the
`ExternalAccountRevoked` / `ExternalAccountDisconnected` contracts.

### Out of scope / future (see [product-plan.md](product-plan.md))

| Feature | Status |
|---|---|
| **Channels reaction to revocation** (Telegram "reconnect needed") | Contracts published; no Channels subscriber/templates yet (phase I2). |
| **`int003` integration event log** | Designed, not created (phase I2). |
| **API-key management endpoints** (register/rotate/remove) + `openai`/`gemini` catalog | The read path (`GetApiKeyAsync`) exists; no way to create an `api_key` account yet (phase I3). |
| **More providers** (Microsoft, CalDAV) | Future, demand-driven (phase I4). |
