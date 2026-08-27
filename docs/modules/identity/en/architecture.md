# Architecture

[← Back to index](../README.md) · Related: [Data Model](data-model.md), [Authentication](authentication.md)

---

## 1. Project layout

Layered projects under `backend/src/Modules/Identity/`:

```
Pottmayer.Pandora.Modules.Identity.
  Abstractions      → module registration + public surface
  Application       → Commands (SignUp, SignIn, RefreshToken, SignOut, Activation, ChangePassword,
                      PasswordReset, Mfa/*, UpsertPreferences, PurgeRefreshTokens),
                      Queries (GetCurrentUser, GetPreferences, Mfa/GetMfaStatus), Dtos, Security, DI
  Contracts         → IntegrationEvents: AccountActivationRequested, AccountActivated,
                      PasswordResetRequested, PasswordChanged, MfaEnabled, MfaDisabled
  Domain            → User aggregate, entities, ValueObjects, Ports (repositories + services), Errors
  Infrastructure    → Argon2 hasher, TOTP authenticator, JWT issuer/validator (Tars), refresh-token
                      purge job, DI
  Persistence       → EntityConfigs, Repositories, IdentityDbContext, DI
  Presentation      → AuthController, MeController, MfaController, PreferencesController, DI
```

## 2. Domain building blocks

### Aggregate & entities (`Domain`)

| Type | Role |
|---|---|
| **User** (aggregate root, `idt001`) | Name, unique username + email, Argon2id `password_hash`, `email_confirmed_at`, `disabled_at`, `mfa_enabled`, sign-in/password timestamps. |
| **UserPreferences** (`idt003`) | Theme, language, `TimeZone` (IANA), `WeekStartsOn` (`DayOfWeek`), `DefaultAlertOffsetMinutes`. One per user. |
| **StoredRefreshToken** (`idt002`) | Hashed, single-use refresh token with claims snapshot, expiry and `consumed_at`. |
| **AccountActivationToken** (`idt004`) | Single-use hashed email-activation token. |
| **PasswordResetToken** (`idt005`) | Single-use hashed reset token. |
| **MfaCredential** (`idt006`) | Encrypted TOTP secret + `confirmed_at`. |
| **MfaRecoveryCode** (`idt007`) | Single-use hashed backup code. |
| **MfaChallenge** (`idt008`) | Short-lived hashed step-up token issued after password success when MFA is on. |

### Value objects (`Domain/ValueObjects`)

`AppTheme` (`light` \| `dark` \| `system`), `AppLanguage` (`pt-BR` \| `en`).

### Ports (`Domain/Ports`)

- **Services:** `IPasswordHasher` (Argon2id — `Argon2PasswordHasher`), `ITotpAuthenticator` (TOTP
  verify/generate), `ISecretProtector` (encrypt the MFA secret — from Tars DataProtection).
- **Repositories:** `IUserRepository`, `IRefreshTokenRepository`, `IActivationTokenRepository`,
  `IPasswordResetTokenRepository`, `IMfaCredentialRepository`, `IMfaRecoveryCodeRepository`,
  `IMfaChallengeRepository`.

## 3. Tars building blocks

- **`Pottmayer.Tars.Security.Identity`** — JWT issue/validate (`AddTarsIdentityJwtTokenIssuer` /
  `…JwtTokenValidator`) and the `IRefreshTokenService`, backed by Identity's `idt002` store. The
  access-token shape, signing and validation are Tars'; the user and the refresh persistence are
  Pandora's.
- **`Pottmayer.Tars.Security.DataProtection`** — `ISecretProtector` (AES-GCM) for the MFA secret.

## 4. Background jobs

`RefreshTokenPurgeBackgroundService` (`PurgeRefreshTokens`) periodically deletes expired and consumed
refresh tokens, keeping `idt002` from growing without bound.

## 5. Contracts (in-process events)

Identity publishes facts; Channels' subscribers turn them into email (Identity never names a template):

`AccountActivationRequested`, `AccountActivated`, `PasswordResetRequested`, `PasswordChanged`,
`MfaEnabled`, `MfaDisabled`.

## 6. Key design decisions

| # | Decision | Rationale |
|---|---|---|
| **1** | Argon2id for passwords. | Memory-hard, modern; resists GPU cracking better than bcrypt/PBKDF2. |
| **2** | Store only hashes of tokens (refresh, activation, reset, challenge, recovery). | A database dump yields no usable token; the plaintext exists only in transit. |
| **3** | Refresh tokens rotate and are single-use (`consumed_at`). | Small replay window; a reused token is detectable. |
| **4** | MFA secret encrypted with a key outside the DB. | A dump alone cannot reconstruct a user's TOTP. |
| **5** | JWT plumbing in Tars, users here. | The token format is reusable across systems (roberto too); the user model is Pandora's. |
| **6** | Security events are contracts, not direct emails. | Identity stays unaware of channels/templates; delivery policy lives in Channels. |

## 7. Cross-cutting rules

- **Anonymous vs. authenticated.** Auth entry points (`signup`, `signin`, `activate`,
  `password/forgot`, `password/reset`, `refresh`, `mfa/challenge`) are anonymous; account,
  preferences and MFA management require a valid access token.
- **Uniform failures.** Sign-in and forgot-password do not reveal account existence.
- **`TimeProvider` everywhere** — token expiry and TTLs are computed against injected time.
