using System.Text.Json;

namespace Pottmayer.Pandora.Desktop.Host;

/// <summary>
/// The script injected into every document. It defines <c>window.pandoraDesktop</c> only on the Pandora
/// origin; the shell checks the origin again on every message, so this check is a courtesy, not the guard.
/// </summary>
internal static class BridgeScript
{
    public static string For(string origin, string version) => Template
        .Replace("__ORIGIN__", JsonSerializer.Serialize(origin))
        .Replace("__VERSION__", JsonSerializer.Serialize(version));

    private const string Template = """
        (() => {
          if (location.origin !== __ORIGIN__ || !window.chrome?.webview) return;
          const webview = window.chrome.webview;
          const pending = new Map();
          const listeners = new Map();
          let seq = 0;

          webview.addEventListener('message', (e) => {
            const m = e.data;
            if (!m || typeof m !== 'object') return;
            if (m.id !== undefined && pending.has(m.id)) {
              const p = pending.get(m.id);
              pending.delete(m.id);
              if (m.error !== undefined) p.reject(new Error(m.error)); else p.resolve(m.result);
            } else if (m.event !== undefined) {
              (listeners.get(m.event) ?? []).forEach((h) => h(m.payload));
            }
          });

          const invoke = (method, args) => new Promise((resolve, reject) => {
            const id = ++seq;
            pending.set(id, { resolve, reject });
            webview.postMessage({ id, method, args });
          });

          window.pandoraDesktop = Object.freeze({
            version: __VERSION__,
            capabilities: () => invoke('desktop.capabilities'),
            invoke,
            on(event, handler) {
              const set = listeners.get(event) ?? [];
              listeners.set(event, [...set, handler]);
              return () => listeners.set(event, (listeners.get(event) ?? []).filter((h) => h !== handler));
            },
          });
        })();
        """;
}
