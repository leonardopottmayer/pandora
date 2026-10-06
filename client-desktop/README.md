# Pandora Desktop

The Windows app that shows the user's Pandora (`client-web`, loaded from the server) in a native
window, and adds what a browser cannot do. Design: [docs/architecture/en/desktop-client.md](../docs/architecture/en/desktop-client.md).

| Project | What it is |
|---|---|
| `src/Pottmayer.Pandora.Desktop.Abstractions` | The module contract (`IDesktopModule`, `IBridgeHandler`, `IBridgeEvents`). Plain `net10.0`. |
| `src/Pottmayer.Pandora.Desktop.Host` | The shell: WinForms + WebView2, tray, bridge, start with Windows, Velopack updates. The only Windows-specific project. |
| `tests/Pottmayer.Pandora.Desktop.Host.Tests` | Bridge routing, origin checks, server URL parsing. |

Version comes from the repo's `/VERSION`, like the backend and the web.

## Run from source

```bash
dotnet run --project client-desktop/src/Pottmayer.Pandora.Desktop.Host
```

First run asks for the server URL — the dev server works too (`localhost:5173`). Settings live in
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

## Release

The [Desktop release](../.github/workflows/desktop-release.yml) workflow builds, tests, packs and
publishes to GitHub Releases. Run it from the Actions tab, or push a tag `v<VERSION>` (it must match
`/VERSION`). Installed apps check those releases at startup and every 6 hours, download a newer
version in the background, and switch to it on the next start.
