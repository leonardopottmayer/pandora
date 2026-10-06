# Pandora Desktop — Desktop Client

> **Status:** Plan. Nothing is built yet. Phase D1 (the shell) is the next thing to be implemented;
> the [Files](../../modules/files/README.md) module comes after it and is the first module to need
> the desktop.
> 🇧🇷 [Versão em português](../pt-BR/desktop-client.md)
>
> Cross-cutting document: the desktop is a client of every module and belongs to none of them.
> Affected: [Files](../../modules/files/en/product-plan.md) (first desktop module) ·
> [Identity](../../modules/identity/README.md) (device credentials, phase D2).
> See also: [Messaging](messaging.md) · [Homelab deploy](../../deployment/homelab-deploy.md)

---

## 1. What it is

**Pandora Desktop** is a Windows app that shows the same `client-web` you use in the browser, inside a
native window, and adds what a browser cannot do: read the local disk, sit in the tray, start with
Windows, keep working with the window closed.

It is a **shell**, not a second front-end. Every screen — including the ones that only make sense on
the desktop — lives in `client-web`. The desktop contributes *capabilities*, exposed to those screens
through a small bridge.

```
┌──────────────────────── server (homelab today) ─────────────────────────┐
│ backend/      modules: Identity, Finances, Notes, ..., Files            │
│ client-web/   every screen, desktop-only ones included                  │
└─────────────────────────────────────────────────────────────────────────┘
          ▲ HTTP: screens (remote load)          ▲ HTTP: data (/api)
          │                                      │
┌──────────────────────── Pandora Desktop (a user's PC) ──────────────────┐
│ Shell (host)         window + WebView2, tray, start with Windows,       │
│                      auto-update, the bridge, the module list           │
│ Desktop modules      one library per native feature, each off by        │
│   Desktop.Files      default: the disk scanner (first one)              │
│   Desktop.<next>     the same contract, when a reason shows up          │
└─────────────────────────────────────────────────────────────────────────┘
```

### What it is not

- **Not a second UI.** No screen is written for the desktop. If a feature needs a page, the page goes
  in `client-web` and asks the bridge whether the capability it needs is there.
- **Not offline-capable.** Pandora's data lives in the backend; without it there is nothing to show.
  The desktop does not cache data or queue writes.
- **Not a Pandora worker.** [Messaging](messaging.md) says there are no separate workers, and that
  stays true: the desktop is a **client** that calls the public API with its own credential, exactly
  like the browser or Telegram. Nothing in the backend depends on it being up.
- **Not a plugin platform.** Desktop modules are compiled in and shipped in the installer; there is
  no runtime loading of third-party code (see D7).

---

## 2. Naming and coordinates

| Thing | Value |
|---|---|
| Folder in the monorepo | `client-desktop/` (sibling of `client-web/`) |
| Solution | `client-desktop/Pottmayer.Pandora.Desktop.slnx` |
| Shell (the `.exe`) | `Pottmayer.Pandora.Desktop.Host` — `net10.0-windows`, WinForms as the container for the WebView2 control and the tray icon (no UI of its own beyond that) |
| Module contract | `Pottmayer.Pandora.Desktop.Abstractions` — `IDesktopModule`, `IBridgeHandler` — plain `net10.0` |
| First module | `Pottmayer.Pandora.Desktop.Files` (phase F1 of [Files](../../modules/files/en/product-plan.md)) — plain `net10.0` |
| Platform rule | **only the shell references Windows**; contract and modules stay cross-platform (see 4.8) |
| Version | the repo's `/VERSION`, same as backend and web (lockstep) |
| Installer / updates | [Velopack](https://velopack.io) — `Setup.exe`, per-user install, updates from GitHub Releases |
| Local data | `%LOCALAPPDATA%\Pandora\` — `settings.json`, `credentials.bin` (DPAPI), `WebView2\` (browser profile) |
| Bridge in JS | `window.pandoraDesktop` — absent in a normal browser |

---

## 3. Principles

1. **The shell knows no feature.** It hosts modules the same way the backend `Host` hosts Finances
   and Notes without knowing their rules. Adding a desktop feature never edits the shell. *(D1)*
2. **One front-end.** All screens live in `client-web`. A page asks the bridge for a *capability*
   ("is `files` active here?"), never "am I on the desktop?". *(D2)*
3. **Screens are loaded remotely.** The shell opens the user's Pandora URL; a front-end change ships
   with the normal web deploy, never as a desktop release. *(D3)*
4. **Everything is opt-in, at two levels.** The account switch lives in the module's settings on the
   server; the device switch lives on the PC. Both start off. An inactive module does not even start.
   *(D4)*
5. **The bridge only answers Pandora.** Native calls are accepted only from the configured server's
   origin. Any other page in the WebView gets no bridge. *(D5)*
6. **Background work has its own credential.** A module that runs with the window closed uses a
   **device credential** — scoped, revocable, never the user's session or password. *(D6)*
7. **Shipped together, off by default.** Every desktop module is in the installer; turning one on is
   a setting, not a download. Real add-ons are deferred until a module is too big to ship to everyone.
   *(D7)*

---

## 4. Architecture

### 4.1 Loading the front-end

- **First run** asks for the server URL (e.g. `http://192.168.1.10:8730` on the LAN) and stores it in
  `settings.json`. The WebView navigates there; the user signs in on the normal login screen, MFA
  included.
- `client-web` keeps its tokens where it already does (the WebView2 profile's storage), so the
  session survives restarts exactly as in a browser. **Nothing changes in the auth flow for D1.**
- **Navigation outside the server's origin** (an external link) opens in the default browser, not in
  the app window.
- **Server unreachable:** the shell shows a small local page — "could not reach Pandora at …, retry /
  change server" — instead of the WebView's raw error. This page is the only HTML shipped with the
  app.

Why remote and not embedded (the `dist/` inside the installer): the only thing embedding would buy is
opening screens without the server, and Pandora's screens are empty without the server. What it would
cost is a desktop release for every UI change and the risk of an old front-end talking to a newer API.

### 4.2 The bridge

The shell injects a script on every document created from the allowed origin, which defines:

```ts
interface PandoraDesktop {
  version: string                                     // the app's /VERSION
  capabilities(): Promise<string[]>                   // active modules on this PC, e.g. ["files"]
  invoke<T>(method: string, args?: unknown): Promise<T>  // "files.pickFolder", "desktop.setAutostart"
  on(event: string, handler: (payload: unknown) => void): () => void  // "files.scanProgress"
}
```

Transport is WebView2's own message channel (`chrome.webview.postMessage` ↔ `WebMessageReceived`),
with a JSON envelope `{ id, method, args }` / `{ id, result | error }`. On every message the shell
checks the sender's origin against the configured server URL and drops anything else (D5).

Method names are **namespaced by module** (`files.*`). The shell owns the `desktop.*` namespace for
its own settings (autostart, server URL, module switches, app version).

In `client-web`, a single hook wraps it — something like `useDesktop()` returning `null` in a browser —
so pages never touch `window.pandoraDesktop` directly and every desktop-only UI degrades to "not
shown" in the browser.

### 4.3 Desktop modules

```csharp
// Pottmayer.Pandora.Desktop.Abstractions
public interface IDesktopModule
{
    string Name { get; }                          // "files" — bridge namespace and settings key
    void Register(IServiceCollection services);   // IHostedService, IBridgeHandler, options...
}

public interface IBridgeHandler
{
    string Method { get; }                        // "files.pickFolder"
    Task<object?> HandleAsync(JsonElement? args, CancellationToken ct);
}
```

- The shell runs a **generic host** (`Microsoft.Extensions.Hosting`). On startup it walks the module
  list — a plain list in the composition root, like `Program.cs` in the backend — and calls
  `Register` **only for modules switched on for this device**. A switched-off module has no services,
  no handlers and no background work. Turning a module on or off restarts the app's host.
- Background work is an ordinary `IHostedService`. It keeps running with the window hidden.
- `capabilities()` returns the names of the registered modules.

### 4.4 The two switches

| Switch | Where | Meaning | Default |
|---|---|---|---|
| **Account** | the module's settings on the server (same shape as Assistant's `is_enabled`) | "I use this feature" — off hides the module everywhere, web and desktop | off |
| **Device** | `settings.json`, under the module's name, edited from a desktop settings screen | "this PC does this feature's native work" — e.g. the PC with the disk scans it, a notebook doesn't | off |

A page shows its desktop-only parts when the account switch is on **and** the capability is in
`capabilities()`. Example: the Files "Watched folders" screen appears on the homelab's desktop app,
not on the notebook's, and not in the browser.

### 4.5 Device credential *(phase D2, needed by Files)*

Background work cannot ride on the user's session: it expires, and it has the user's full reach. A
module that needs to call the API from the background uses a device credential instead.

- **Owned by Identity**, because it is authentication and is shared by every future desktop module:
  a new table (`idt0XX_device`) with `user_id`, device name, `platform` (`windows` | `linux` |
  `macos` | `android` | `ios`), `form` (`desktop` | `headless` | `mobile`), the **hash** of the key,
  granted **scopes** (e.g. `files.agent`), `last_seen_at`, `revoked_at`.
- **Pairing, from inside the app, already signed in:** the module's page offers "Use this PC as a
  file agent". The web calls `POST /identity/devices` with the user's normal session and the scope it
  needs; the backend returns the key **once**; the page hands it to the bridge
  (`desktop.storeCredential`), which encrypts it with **DPAPI** (current Windows user) into
  `credentials.bin`. The key exists in plaintext only in that one response.
- **Use:** the module sends it as `X-Api-Key`. Tars already has the scheme —
  `AddTarsIdentityApiKey` + `ApiKeyAuthenticationHandler`, which calls an `IApiKeyValidator` that
  Pandora implements (hash lookup → principal with the user id and the scope claims). Pandora does not
  use this scheme today.
- **Reach:** the default authorization policy stays JWT-only. A device key is accepted **only** on
  endpoints that opt in with the device scheme plus a scope policy (Files' `/files/agent/*` requires
  `files.agent`). A stolen key cannot read Finances.
- **Revocation:** a "Connected devices" list in the web settings (Identity). Revoking makes the next
  call 401, and the module shows itself as disconnected until paired again.
- **Pairing without a screen** (headless host, 4.8): the host asks the backend for a short code,
  prints it with the URL, and polls; the user confirms the code in the web, already signed in; the
  host then receives its key. This is the device authorization flow (the "TV login" pattern). The
  desktop does not need it — it is already signed in.

### 4.6 Lifecycle

- **Tray:** closing the window hides it; the tray menu has *Open*, *Settings* and *Quit*. *Quit*
  stops the host, so background modules stop too.
- **Single instance:** a second launch focuses the existing window.
- **Start with Windows:** a toggle (`desktop.setAutostart`) writing the `HKCU\…\Run` key — no admin.
  Starts hidden in the tray.
- **Updates:** Velopack checks GitHub Releases at startup and periodically, downloads in the
  background and applies on the next restart. A failed update leaves the current version running.

### 4.7 Where it runs against

Today the server is the homelab (Windows, Docker), reachable on the LAN — see
[homelab deploy](../../deployment/homelab-deploy.md). With the public phase (Cloudflare Tunnel +
Cloudflare Access) the WebView passes Access like any browser, but the **background calls** of a
module do not have a browser session for Access; they will need either an Access service token or an
Access bypass for the device-key routes (open question 2).

---

### 4.8 Other platforms

The split that keeps this cheap is already in 4.3: modules only know `IServiceCollection`,
`IHostedService` and `IBridgeHandler`, and target plain `net10.0`, which runs on Windows, Linux and
macOS. Only the shell is Windows-specific. Each new platform is therefore a new **host**, not a
rewrite of the modules.

| Platform | Host | What it takes | Effort |
|---|---|---|---|
| Windows (D1) | WinForms + WebView2 | — | the baseline |
| **Headless** — Linux server/NAS, Docker, Windows service | a console host (generic host + systemd / Windows Service integration): no window, no bridge | pairing by code (4.5); configuration already lives on the server (e.g. Files' roots and filters), so nothing is set up locally; key stored in a file readable only by the service user | small — the most useful next step |
| Linux / macOS desktop | another shell around the system webview (e.g. Photino.NET over WebKitGTK / WKWebView) or Avalonia | tray, autostart (`.desktop` file / LaunchAgent), packaging (AppImage / `.dmg`), DPAPI → libsecret / Keychain | medium, per OS |
| Android / iOS | a mobile shell wrapping the same `client-web` (e.g. Capacitor) plus native plugins | the OS restricts both sides: no free walk of the filesystem (photo library and user-granted folders only), and background work is rationed — iOS above all runs it when the system decides. Native code in another language, so modules are reimplemented, not reused | large |

**Recommendation.** D1 stays Windows-only — the homelab is Windows — under the rule that nothing but
the shell references Windows. The first other platform worth building is the **headless host**: it
covers Linux servers and NAS boxes, and also the "nobody signed in" case on Windows (open question 1).
Desktop shells for Linux/macOS come when there is such a machine to run them on. Phones come last,
and for Files their useful scope is "the phone's photos and videos", not arbitrary folders.

---

## 5. Failure behaviour

| Situation | Behaviour |
|---|---|
| Server unreachable | local "could not reach Pandora" page with *Retry* / *Change server*; background modules back off and retry |
| Session expired | handled by `client-web` as today (refresh, else the login screen) |
| Device key revoked or invalid (401) | the module stops its work, marks itself disconnected, the page offers pairing again |
| Bridge call from a foreign origin | dropped and logged; the page never sees a reply |
| Update fails | current version keeps running; retried on the next check |

---

## 6. Roadmap

### Phase D1 — The shell *(next)*

- `client-desktop/` with `Desktop.Host` and `Desktop.Abstractions`; empty module list.
- First-run server URL, remote load, external links to the default browser, offline page.
- Tray, single instance, start with Windows (off by default).
- Bridge with `version`, `capabilities()` (empty) and the `desktop.*` methods; origin check;
  `useDesktop()` hook in `client-web` plus a small "Desktop" section in the web settings (autostart,
  server, version) shown only inside the app.
- Velopack packaging and a CI job publishing `Setup.exe` + updates on a release tag.
- **Done when:** you install it, sign in, use Pandora exactly as in the browser; closing the window
  keeps it in the tray; restarting Windows brings it back; a new release updates it by itself.

### Phase D2 — Device credentials *(prerequisite of Files F1)*

- Identity: `idt0XX_device`, `POST/GET/DELETE /identity/devices`, `IApiKeyValidator`, scope policies;
  `AddTarsIdentityApiKey` registered alongside JWT without changing the default policy.
- Shell: `desktop.storeCredential` + DPAPI storage; an authenticated `HttpClient` for modules.
- Web: "Connected devices" in the settings.
- **Done when:** a device key reaches an endpoint that opts in with its scope, gets 401 on every other
  endpoint, and stops working the moment it is revoked.

### Later — when a reason shows up

- `Desktop.Assistant`: a global hotkey opening the Assistant command bar anywhere in Windows.
- `Desktop.Agenda`: native Windows notifications for reminders, as an alternative channel to
  Telegram.
- Drop a file on the tray icon to send it to the Finances inbox queue.
- **D3 — Headless host** (4.8): the same modules as a console/daemon host on Linux, Docker or as a
  Windows service, with pairing by code. Also the answer to open question 1.
- Linux / macOS desktop shells; mobile shells (4.8).

---

## 7. Open questions

1. **Signed-in session on the homelab.** A tray app only runs after a Windows sign-in. The homelab
   runs Docker Desktop, which already requires one, so the tray is assumed to be enough. If that
   changes, the headless host (D3) runs the same modules as a Windows service and the window becomes
   UI only — the module contract does not change, only which process hosts it.
2. **Cloudflare Access and background calls** (see 4.7): service token vs. bypass on the device-key
   routes. Only matters once the public phase is live; on the LAN it does not apply.
3. **Code signing.** An unsigned `Setup.exe` triggers SmartScreen. Acceptable for personal use;
   revisit if the app is shared with other people.
4. **Shell technology for Linux/macOS.** Photino.NET, Avalonia, Tauri or Electron — decided when
   that phase starts, against what is mature then. The module contract and the bridge stay either
   way.
