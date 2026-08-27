# Implementation Status

[← Back to index](../README.md)

A snapshot of what is built in the codebase versus what is designed but not yet implemented.

---

## Implemented

| Area | Notes |
|---|---|
| **Module scaffold** | Seven layered projects; `identity` schema; `idt001`–`idt008`. |
| **User + password** | `User` aggregate; Argon2id hashing (`Argon2PasswordHasher`); unique username/email. |
| **Sign-up + activation** | `SignUp`; `AccountActivationRequested` → email; `activate` consumes `idt004`; `AccountActivated`. |
| **Sign-in** | Argon2id verify; uniform failure; JWT access token + rotating refresh token (`idt002`), via Tars. |
| **Refresh + sign-out** | `refresh` rotates single-use tokens (`consumed_at`); `signout` consumes; `RefreshTokenPurgeBackgroundService` cleans up. |
| **Password reset/change** | `forgot` (uniform) → `PasswordResetRequested`; `reset` consumes `idt005`; authenticated `change`; `PasswordChanged`. |
| **MFA (TOTP)** | `setup`/`enable`/`disable`/`status`; secret encrypted (`idt006`, `ISecretProtector`); recovery codes hashed single-use (`idt007`); sign-in challenge (`idt008`); `MfaEnabled`/`MfaDisabled`. |
| **Preferences** | `idt003` — theme, language, **time zone, week start, default alert offset**; `GET`/`PUT` with validation. |
| **Contracts** | Six security events consumed by Channels' subscribers. |
| **Frontend** | `client-web/src/modules/identity` — sign-in, sign-up, MFA, preferences. |

## Notable facts for other modules

- **Identity carries the IANA time zone, week start and default alert offset** (`idt003`) — the trio
  the Agenda plan listed as a "phase 0" prerequisite. It is **built and exposed** via
  `PUT /identity/preferences`. Consuming it fully (Agenda item defaults, Channels quiet hours) is
  follow-up work in *those* modules, not here. See [Preferences](preferences.md).
- **Security emails** flow entirely through Channels' fact→template path; Identity names no template.

## Not yet implemented (future)

| Area | Status |
|---|---|
| **Social / OAuth login** (e.g. Google sign-in) | Not implemented. (Distinct from [Integrations](../../integrations/en/overview.md), which is Pandora calling Google *as* the user.) |
| **Per-device session management UI** | Refresh tokens are stored + purged, but there is no session list / revoke-per-device screen. |
| **WebAuthn / passkeys** | Future — MFA is TOTP-only today. |
| **Roles / permissions** | Not modelled — single-user personal system. |
