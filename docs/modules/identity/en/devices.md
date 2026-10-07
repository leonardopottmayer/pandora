# Devices

[← Back to index](../README.md) · Related: [Authentication](authentication.md), [Data Model](data-model.md#idt009_device), [Pandora Desktop](../../../architecture/en/desktop-client.md)

---

A **device** is a paired client — Pandora Desktop today; a headless agent or a phone later — that
calls the API with **its own key** instead of the user's session. Background work (the Files agent
scanning a disk with the window closed) cannot ride on a session: sessions expire, and they carry the
user's whole reach. A device key is long-lived, **limited to its scopes**, and **revocable** at any
time.

## 1. Pairing

The user is signed in; the device is paired from the web, with the session:

```
POST /identity/devices  { name, platform, form, scopes[] }
→ { device, key }        ← the key appears here once, never again
```

- `platform`: `windows` | `linux` | `macos` | `android` | `ios`. `form`: `desktop` | `headless` | `mobile`.
- `scopes`: lower-case, dot-separated names (`files.agent`). They are **not** checked against a
  catalog — the user grants their own device access to their own data, and a scope only means
  something where an endpoint asks for it.
- The key is `pdk_` + 32 random bytes (base64url). Only its **SHA-256** is stored (`idt009.key_hash`),
  like every other token in this module.

Inside Pandora Desktop, **Account → Devices** shows "Connect this computer": the page registers the
device with the computer's name and hands the key to the app through the bridge
(`desktop.storeCredential`), which encrypts it with DPAPI. See
[desktop-client §4.5](../../../architecture/en/desktop-client.md#45-device-credential-phase-d2-needed-by-files).

## 2. Authenticating with a key

The device sends `X-Api-Key: pdk_…`. The scheme is Tars' (`AddTarsIdentityApiKey`), registered under
the name `ApiKey` next to JWT. Pandora's `DeviceApiKeyValidator` hashes the key, finds the
non-revoked device and returns a principal with:

| Claim | Value |
|---|---|
| `Id` (and the name identifier) | the **user's** id — so the user context and every per-user query work exactly as with a session |
| `device_id` | the device's id |
| `scope` | one claim per granted scope |

Each successful request records `last_seen_at`, at most once every 5 minutes.

## 3. Which endpoints accept a key

The **default policy stays JWT-only**: `[Authorize]` never accepts a device key. An endpoint opts in by
naming the device scheme and a device policy — the names live in `Identity.Abstractions`
(`DeviceAuthorization`) so any module can use them:

```csharp
[Authorize(AuthenticationSchemes = DeviceAuthorization.Scheme, Policy = DeviceAuthorization.Policy)]                           // any device
[Authorize(AuthenticationSchemes = DeviceAuthorization.Scheme, Policy = DeviceAuthorization.ScopePolicyPrefix + "files.agent")] // that scope
```

The policies are built on demand by `DevicePolicyProvider` (nothing to register per scope); both also
require the `device_id` claim, so a session token never satisfies them. The consequence, pinned by the
integration tests: a key gets **401 on every session endpoint**, and a session gets **401 on every
device endpoint**.

## 4. Listing and revoking

`GET /identity/devices` lists the user's **active** devices (newest first). `DELETE
/identity/devices/{id}` revokes: `revoked_at` is set and the key fails on its next request. Revoked
devices leave the list; their rows stay. Another user's device answers 404.

Pandora Desktop checks its key at startup (`GET /identity/devices/me`) and forgets it on a 401, so a
computer revoked from elsewhere shows as "not connected" the next time it opens.
