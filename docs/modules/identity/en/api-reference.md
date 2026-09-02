# API Reference

[← Back to index](../README.md) · Related: [Authentication](authentication.md), [MFA](mfa.md)

Base path: **`/api/v{version}/identity`**. Auth entry points are **anonymous**; account, preferences
and MFA management require a valid access token. Errors are mapped from typed `Result` failures, with
**uniform** messages where account existence must not leak.

---

## Auth — `/identity/auth`

| Method | Path | Auth | Purpose |
|---|---|---|---|
| POST | `/auth/signup` | anon | Create an account (unconfirmed); publish `AccountActivationRequested`. |
| POST | `/auth/signin` | anon | Verify password; issue tokens, **or** an MFA challenge if MFA is on. |
| POST | `/auth/activate` | anon | Consume the activation token; confirm the email. |
| POST | `/auth/password/forgot` | anon | Publish `PasswordResetRequested` (uniform response). |
| POST | `/auth/password/reset` | anon | Consume the reset token; set a new password. |
| POST | `/auth/password/change` | user | Change password (verifies the current one). |
| POST | `/auth/refresh` | anon | Rotate: consume the refresh token, issue a new access + refresh pair. |
| POST | `/auth/signout` | user | Consume the current refresh token. |

## Current user — `/identity`

| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/me` | user | The signed-in user's profile. |

## MFA — `/identity/mfa`

| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/mfa/status` | user | Is MFA enabled, plus the count of unused recovery codes. |
| POST | `/mfa/setup` | user | Generate + store (encrypted) a TOTP secret; return provisioning data. |
| POST | `/mfa/enable` | user | Confirm a TOTP code; enable MFA; return recovery codes once; publish `MfaEnabled`. |
| POST | `/mfa/disable` | user | Disable MFA (re-verifying a factor); publish `MfaDisabled`. |
| POST | `/mfa/recovery-codes` | user | Regenerate recovery codes. |
| POST | `/mfa/challenge` | anon | Exchange a sign-in MFA challenge + TOTP/recovery code for tokens. |

## Preferences — `/identity/preferences`

| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | `/preferences` | user | Read the user's preferences. |
| PUT | `/preferences` | user | Upsert theme / language / time zone / week start / default alert offset. |

---

## Contracts (in-process events)

Published for [Channels](../../channels/en/overview.md) to turn into email:

`AccountActivationRequested`, `AccountActivated`, `PasswordResetRequested`, `PasswordChanged`,
`MfaEnabled`, `MfaDisabled`.
