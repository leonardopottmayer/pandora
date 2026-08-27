# Overview — Business & Principles

[← Back to index](../README.md) · Related: [Architecture](architecture.md), [Authentication](authentication.md)

---

## 1. What the module does

**Identity** owns *who the user is* and how they prove it:

- **Account lifecycle** — sign-up, email activation, disable.
- **Authentication** — sign-in issuing a short-lived **JWT access token** and a **rotating refresh
  token**; refresh; sign-out.
- **Password management** — forgot/reset (via emailed token) and authenticated change.
- **Multi-factor authentication** — TOTP enrolment, a step-up **challenge** at sign-in, and single-use
  **recovery codes**.
- **Preferences** — theme, language, IANA time zone, week start, and the default alert offset used by
  Agenda.

Security-relevant events (activation, password reset/change, MFA enable/disable) are published as
**contracts** that [Channels](../../channels/en/overview.md) turns into email. Identity itself never
knows a template exists.

## 2. Core principles

1. **Never store a reusable secret in the clear.** Passwords are hashed with **Argon2id**; refresh
   tokens and every one-time token (activation, reset, MFA challenge, recovery code) are stored as
   **SHA-256 hashes**; the TOTP secret is **encrypted at rest** (`ISecretProtector`).
2. **Tokens are issued and validated by Tars; users are owned here.** The JWT machinery lives in
   `Pottmayer.Tars.Security.Identity`; Identity supplies the user, the claims, and the persistence for
   refresh tokens (`idt002`).
3. **Refresh tokens are single-use and rotate.** Each refresh consumes the presented token
   (`consumed_at`) and issues a new one, so a stolen-and-replayed refresh token is detectable and the
   window is small.
4. **One-time tokens are single-use and short-lived.** Activation, reset, and MFA challenge tokens are
   consumed on use and expire; they authenticate exactly one action.
5. **Auth failures are uniform.** Sign-in does not reveal whether the email exists or the password was
   wrong; forgot-password does not reveal whether an account exists.

## 3. Ubiquitous language (glossary)

| Term | Meaning |
|---|---|
| **User** (`idt001`) | The account: name, unique username + email, Argon2id password hash, activation state, MFA flag. |
| **Access token** | A short-lived JWT carrying the user's claims. Issued at sign-in / refresh / MFA completion. |
| **Refresh token** (`idt002`) | A long-lived, single-use, **hashed** token that mints a new access token; rotated on every use. |
| **Activation token** (`idt004`) | A single-use hashed token emailed at sign-up to confirm the email. |
| **Password reset token** (`idt005`) | A single-use hashed token emailed on "forgot password". |
| **MFA credential** (`idt006`) | The user's TOTP secret, **encrypted**; `confirmed_at` marks a completed enrolment. |
| **Recovery code** (`idt007`) | A single-use hashed backup code for when the authenticator is unavailable. |
| **MFA challenge** (`idt008`) | A short-lived hashed token issued after password success when MFA is on; exchanged for the access token by a valid TOTP/recovery code. |
| **Preferences** (`idt003`) | Per-user UI + scheduling defaults: theme, language, time zone, week start, default alert offset. |

## 4. Scope

### In scope (implemented — see [Implementation Status](implementation-status.md))

The `identity` schema (`idt001`–`idt008`); sign-up + email activation; sign-in with JWT access +
rotating refresh tokens; refresh; sign-out; forgot/reset and authenticated change of password; TOTP
MFA (setup/enable/disable, status, recovery codes, sign-in challenge); user preferences
(theme/language/time zone/week start/default alert offset); the security-event contracts consumed by
Channels; a background purge of expired/consumed refresh tokens; and the frontend.

### Out of scope / future

| Feature | Status |
|---|---|
| **Social / OAuth login** (Google sign-in) | Not implemented. Note: *Pandora calling Google as the user* lives in [Integrations](../../integrations/en/overview.md); social login would be a separate Identity concern. |
| **Multiple sessions / device management UI** | Refresh tokens are stored and purged, but there is no per-device session list. |
| **Roles / permissions** | Single-user personal system; no role model. |
| **WebAuthn / passkeys** | Future; the MFA model is TOTP-only today. |
