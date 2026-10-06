/**
 * The bridge Pandora Desktop injects into the page (see docs/architecture/en/desktop-client.md).
 * It exists only inside the desktop app, and only on the Pandora origin.
 */
export interface PandoraDesktop {
  version: string
  /** Desktop modules active on this device, e.g. ["files"]. */
  capabilities(): Promise<string[]>
  /** Calls a native method, namespaced by module: "desktop.setAutostart", "files.pickFolder". */
  invoke<T = unknown>(method: string, args?: unknown): Promise<T>
  /** Subscribes to an event pushed by the app; returns the unsubscribe function. */
  on(event: string, handler: (payload: unknown) => void): () => void
}

declare global {
  interface Window {
    pandoraDesktop?: PandoraDesktop
  }
}

/**
 * The desktop bridge, or null in a browser. Pages ask for a capability through it rather than
 * asking "am I on the desktop?", and render nothing desktop-only when it is null. The bridge is
 * defined before the page's scripts run and never changes, so this needs no state.
 */
export function useDesktop(): PandoraDesktop | null {
  return window.pandoraDesktop ?? null
}

/** What `desktop.getSettings` returns. */
export interface DesktopSettings {
  version: string
  serverUrl: string | null
  autostart: boolean
}
