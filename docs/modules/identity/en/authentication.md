# Authentication

[← Back to index](../README.md) · Related: [MFA](mfa.md), [Data Model](data-model.md)

---

## 1. Sign-up & activation

`POST /identity/auth/signup` creates a `User` with an Argon2id password hash, in an **unconfirmed**
state (`email_confirmed_at` NULL), and publishes **`AccountActivationRequested`** — Channels emails a
single-use activation link backed by `idt004`.

`POST /identity/auth/activate` consumes the activation token (single-use, expiring), sets
`email_confirmed_at`, and publishes **`AccountActivated`**. Username and email are unique.

## 2. Sign-in

`POST /identity/auth/signin`:

1. Look up the user by email; verify the password with Argon2id. Failure is **uniform** — the response
   does not reveal whether the email exists or the password was wrong.
2. **If MFA is off** — issue a JWT **access token** (via Tars) and a **refresh token** (persisted
   hashed in `idt002`), stamp `last_sign_in_at`, return `TokenDto`.
3. **If MFA is on** — do *not* issue an access token. Issue a short-lived **MFA challenge** (`idt008`)
   and return it; the client completes it at `POST /identity/mfa/challenge`. See [MFA](mfa.md).

## 3. Access + refresh tokens

- The **access token** is a short-lived JWT carrying the user's claims; it is validated on every
  authenticated request by the Tars JWT validator.
- The **refresh token** is long-lived, **single-use**, and stored **hashed** (`idt002.token_hash`)
  with a claims snapshot.

`POST /identity/auth/refresh` presents the refresh token: it is looked up by hash, checked unconsumed
and unexpired, **consumed** (`consumed_at`), and a **new** access + refresh pair is issued
(rotation). A replayed (already-consumed) refresh token fails, which is how theft is detectable.

`RefreshTokenPurgeBackgroundService` periodically deletes expired/consumed rows.

## 4. Sign-out

`POST /identity/auth/signout` (authenticated) consumes the current refresh token so it can no longer
rotate. The access token remains valid until it expires (stateless JWT), which its short lifetime
bounds.

## 5. Password management

- **Forgot** — `POST /identity/auth/password/forgot` publishes **`PasswordResetRequested`** (Channels
  emails a single-use reset link backed by `idt005`). The response is **uniform** and does not reveal
  whether the account exists.
- **Reset** — `POST /identity/auth/password/reset` consumes the reset token, sets a new Argon2id hash,
  **revokes every refresh token for the user** (all sessions are signed out, since the reset implies the
  old credentials were compromised), and publishes **`PasswordChanged`**.
- **Change** — `POST /identity/auth/password/change` (authenticated) verifies the current password,
  sets the new hash, stamps `last_password_changed_at`, **revokes every refresh token for the user**
  (other devices must re-authenticate), and publishes **`PasswordChanged`**.

## 6. Current user

`GET /identity/me` (authenticated) returns the signed-in user's profile (`GetCurrentUser`) — the SPA's
bootstrap read.
