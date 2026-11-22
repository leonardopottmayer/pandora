# MFA (TOTP)

[← Back to index](../README.md) · Related: [Authentication](authentication.md), [Data Model](data-model.md)

---

Identity's multi-factor authentication is **TOTP** (authenticator-app codes), with single-use
**recovery codes** as a backup and a **step-up challenge** at sign-in.

## 1. Enrolment

1. `GET /identity/mfa/status` — returns whether MFA is enabled and, if so, the count of unused
   recovery codes (`MfaStatusDto`: `Enabled`, `RemainingRecoveryCodes`). It does not expose whether a
   setup is pending.
2. `POST /identity/mfa/setup` — generates a TOTP secret, stores it **encrypted** (`idt006.secret_cipher`,
   `confirmed_at` NULL), and returns the provisioning data (secret / otpauth URI) for the authenticator
   app's QR code. Fails if MFA is already enabled. Calling it again before confirming replaces the
   previous unconfirmed credential with a fresh secret.
3. `POST /identity/mfa/enable` — the user submits a current TOTP code; on success `confirmed_at` is set,
   `user.mfa_enabled = true`, a set of **recovery codes** is generated (stored **hashed**, `idt007`),
   and **`MfaEnabled`** is published (Channels emails a confirmation). The plaintext recovery codes are
   returned **once**, here, and never again.

## 2. Sign-in challenge

When `mfa_enabled` is true, `signin` stops after the password check and issues a short-lived **MFA
challenge** (`idt008`, hashed, expiring) instead of an access token.

`POST /identity/mfa/challenge` exchanges the challenge token **plus** a valid factor for the real
tokens:

- a current **TOTP code** (verified by `ITotpAuthenticator`), or
- a **recovery code** (looked up by hash in `idt007`, consumed single-use).

On success the challenge is consumed and a JWT access + refresh pair is issued — the same output as a
non-MFA sign-in.

## 3. Recovery codes

`POST /identity/mfa/recovery-codes` (authenticated) regenerates the set, invalidating the old codes and
returning the new plaintext set once. Each code is single-use (`consumed_at`).

## 4. Disabling

`POST /identity/mfa/disable` (authenticated, re-verifying a factor) sets `user.mfa_enabled = false`,
removes the credential and recovery codes, and publishes **`MfaDisabled`** (Channels emails a
confirmation).

## 5. Security properties

- The TOTP secret is **encrypted at rest** (`ISecretProtector`, key outside the DB).
- Recovery codes and challenge tokens are **hashed** and **single-use**.
- Enabling/disabling MFA always emits a security email, so a silent change is visible to the user.
