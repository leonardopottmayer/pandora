# Architecture

[← Back to index](../README.md) · Related: [Data Model](data-model.md), [OAuth & Credentials](oauth-and-credentials.md)

---

## 1. Project layout

The module mirrors the other Pandora modules, split into layered projects under
`backend/src/Modules/Integrations/`:

```
Pottmayer.Pandora.Modules.Integrations.
  Abstractions      → public ports for other modules: IExternalCredentialProvider,
                      IExternalAccountReader, ExternalAccessToken, ExternalAccountSummary,
                      IntegrationsModule registration, IntegrationsOptions
  Application       → Commands (StartConnection, HandleCallback, DisconnectAccount),
                      Queries (GetProviders, GetAccounts), the OAuth services
                      (ExternalCredentialProvider, ExternalAccountReader, OAuthProviderRegistry,
                      PkceCodes, ScopeString), DTOs, DI
  Contracts         → IntegrationEvents: ExternalAccountRevoked, ExternalAccountDisconnected
  Domain            → Aggregates (ExternalAccount, OAuthState), ValueObjects (AccountStatus,
                      AuthKind), Ports (IOAuthProvider, repositories), Errors
  Infrastructure    → Provider adapters (Google), DI
  Persistence       → EntityConfigs, Repositories, DbContext, DI
  Presentation      → IntegrationsController, DI
```

Design style: **DDD aggregates** with private constructors + static factories, a `TimeProvider`
injected for all time reads, and a **command/query** application layer (one folder per use case).

## 2. Domain building blocks

### Aggregates (`Domain/Aggregates`)

| Aggregate root | Responsibility / key invariants |
|---|---|
| **ExternalAccount** | One connected account. Holds encrypted credentials; transitions between `connected`/`expired`/`revoked`/`needs_consent`; `MarkRevoked` is the terminal degradation on a rejected refresh. Unique per `(user_id, provider, provider_account_id)`. |
| **OAuthState** | One in-flight authorization request. Carries the CSRF `state` and the encrypted PKCE verifier; single-use, TTL-bounded; consumed exactly once by the callback. |

### Value objects (`Domain/ValueObjects`)

- **`AccountStatus`** — `connected` \| `expired` \| `revoked` \| `needs_consent`.
- **`AuthKind`** — `oauth` (refreshable tokens, Google) \| `api_key` (static user key, OpenAI/Gemini).

### Ports (`Domain/Ports`)

- **`IOAuthProvider`** — one per provider: `BuildAuthorizationUrl`, `ExchangeCodeAsync`,
  `RefreshAsync`, `RevokeAsync`. Resolved by name through `OAuthProviderRegistry`.
- **Repositories:** `IExternalAccountRepository`, `IOAuthStateRepository`.

### Published ports (`Abstractions/Ports`)

The only surface other modules reference:

```csharp
public interface IExternalCredentialProvider
{
    // auth_kind = oauth — refreshes invisibly
    Task<Result<ExternalAccessToken>> GetAccessTokenAsync(Guid userId, string provider, CancellationToken ct = default);
    // auth_kind = api_key — decrypts and returns; no expiry, no refresh
    Task<Result<string>> GetApiKeyAsync(Guid userId, string provider, CancellationToken ct = default);
}

public interface IExternalAccountReader
{
    Task<IReadOnlyList<ExternalAccountSummary>> ListAsync(Guid userId, CancellationToken ct = default);
    Task<ExternalAccountSummary?> GetAsync(Guid externalAccountId, CancellationToken ct = default);
}
```

`ExternalAccessToken` carries the token string, its expiry and the granted scopes. It is a transient
value — never persisted by the consumer, never logged.

### From Tars

`Pottmayer.Tars.Security.DataProtection` supplies `ISecretProtector` (AES-GCM over a key from
configuration/secret store): `Protect(string) → ciphertext`, `Unprotect(string) → plaintext`. Every
credential column passes through it.

## 3. Key design decisions

| # | Decision | Rationale (rejected alternative) |
|---|---|---|
| **I1** | Consumers get a short-lived access token through a port; there is no way to read a refresh token out of the module. | A refresh token in a consumer is a second copy of the crown jewels. |
| **I2** | Refresh is transparent inside `GetAccessTokenAsync` and **serialized per account by an in-process gate** (not a Postgres advisory lock). Two concurrent sync jobs must not burn two refreshes — some providers rotate and invalidate the previous refresh token. | The monolith runs one process, so an in-process gate is enough and cheaper than a DB round-trip. Revisit if the host is ever scaled out. |
| **I3** | Credentials are encrypted at rest with a key sourced **outside** the database (`ISecretProtector`). | A database dump alone must not yield working Google credentials. |
| **I4** | A provider is a config entry + an `IOAuthProvider` adapter, resolved by name. | Adding Microsoft doesn't touch the domain. |
| **I5** | A rejected refresh (`invalid_grant`) marks the account `revoked`, publishes `ExternalAccountRevoked`, and returns a typed failure. | A background failure must degrade cleanly, not loop or crash. |

## 4. Cross-cutting rules

- **Multi-tenant by user.** Every table has `user_id NOT NULL`; every authenticated endpoint is scoped
  to the token's user.
- **The callback is the only anonymous endpoint.** It authenticates by the single-use `state` it
  issued itself — never by a session.
- **`TimeProvider` everywhere.** Token expiry and state TTL are computed against injected time.
