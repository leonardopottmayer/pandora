# Pandora Desktop

The Windows app that shows the user's Pandora (`client-web`, loaded from the server) in a native
window, and adds what a browser cannot do. Design: [docs/architecture/en/desktop-client.md](../docs/architecture/en/desktop-client.md).

| Project | What it is |
|---|---|
| `src/Pottmayer.Pandora.Desktop.Abstractions` | The module contract (`IDesktopModule`, `IBridgeHandler`, `IBridgeEvents`, `IDesktopShell`). Plain `net10.0`. |
| `src/Pottmayer.Pandora.Desktop.Files` | The [Files](../docs/modules/files/README.md) agent: walks the roots, fingerprints, runs the scan protocol on schedule or on "Scan now". Plain `net10.0`; shares the protocol and the selection/filter engine with the backend (`Modules.Files.Agent`). |
| `src/Pottmayer.Pandora.Desktop.Host` | The shell: WinForms + WebView2, tray, bridge, start with Windows, Velopack updates. The only Windows-specific project. |
| `tests/Pottmayer.Pandora.Desktop.Files.Tests` | The walk over a real folder tree, the schedule, the scan protocol against a fake server. |
| `tests/Pottmayer.Pandora.Desktop.Host.Tests` | Bridge routing, origin checks, server URL parsing. |

Modules are off on every PC until switched on (`desktop.setModule`, from the web's settings); the
switch restarts the app.

Version comes from the repo's `/VERSION`, like the backend and the web.

## Run from source

```bash
dotnet run --project client-desktop/src/Pottmayer.Pandora.Desktop.Host
```

First run asks for the server URL — the dev server works too (`localhost:5173`): it proxies `/api` to
the backend (`VITE_API_URL`), as nginx does in production, so desktop modules reach the API on the
same origin. Settings live in
`%LOCALAPPDATA%\Pandora\` (`settings.json`, and `WebView2\` with the browser profile and the session);
delete the folder to start over. Updates are skipped when running from `bin/`.

To inspect the page with Edge DevTools, right-click inside the window, or start the app with
`WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222` and open `edge://inspect`.

## Build the installer locally

```bash
dotnet publish client-desktop/src/Pottmayer.Pandora.Desktop.Host -c Release -r win-x64 --self-contained -o client-desktop/publish
```

```bash
dotnet dnx -y vpk --version 1.2.161 pack --packId PandoraDesktop --packVersion 0.1.0 --packDir client-desktop/publish --mainExe PandoraDesktop.exe --packTitle Pandora --icon client-desktop/src/Pottmayer.Pandora.Desktop.Host/Assets/pandora.ico -o client-desktop/Releases
```

`client-desktop/Releases/PandoraDesktop-win-Setup.exe` installs per user (no admin) into
`%LOCALAPPDATA%\PandoraDesktop`.

## Test an update locally (no tag, no GitHub)

The app updates from the folder in `PANDORA_DESKTOP_UPDATES` instead of GitHub when that variable is
set. Install `0.1.0` with the two commands above, then build a newer version into the same `Releases`
folder (`-p:Version` overrides `/VERSION` for this build only):

```bash
dotnet publish client-desktop/src/Pottmayer.Pandora.Desktop.Host -c Release -r win-x64 --self-contained -p:Version=0.1.1 -o client-desktop/publish
```

```bash
dotnet dnx -y vpk --version 1.2.161 pack --packId PandoraDesktop --packVersion 0.1.1 --packDir client-desktop/publish --mainExe PandoraDesktop.exe --packTitle Pandora --icon client-desktop/src/Pottmayer.Pandora.Desktop.Host/Assets/pandora.ico -o client-desktop/Releases
```

Quit the installed app from the tray, set the variable for your user, and open it from the Start
menu (a new process picks up the variable). It downloads `0.1.1`; quit and open it again and
Preferences → Pandora Desktop shows `0.1.1`.

```powershell
[Environment]::SetEnvironmentVariable('PANDORA_DESKTOP_UPDATES', "$PWD\client-desktop\Releases", 'User')
```

Remove the variable afterwards (`... , $null, 'User'`), and uninstall from Windows Settings → Apps.

## Release

The [Desktop release](../.github/workflows/desktop-release.yml) workflow builds, tests, packs and
uploads to GitHub Releases:

- **Run it from the Actions tab** → a **draft** release. No tag is created and installed apps do not
  see it — use it to test the pipeline and download the CI's `Setup.exe`, then delete the draft.
- **Push a tag `v<VERSION>`** (it must match `/VERSION`) → a published release.

Installed apps check the published releases at startup and every 6 hours, download a newer version
in the background, and switch to it on the next start.
