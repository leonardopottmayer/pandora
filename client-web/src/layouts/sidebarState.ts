const COLLAPSED_KEY = 'pandora.sidebar_collapsed'
const CLOSED_MODULES_KEY = 'pandora.sidebar_closed_modules'

export function loadCollapsed(): boolean {
  try {
    return localStorage.getItem(COLLAPSED_KEY) === 'true'
  } catch {
    return false
  }
}

export function saveCollapsed(collapsed: boolean): void {
  try {
    localStorage.setItem(COLLAPSED_KEY, String(collapsed))
  } catch {
    // Without storage, it only goes back to the default on the next load.
  }
}

/** The closed ones, not the open ones, so a module added later starts open like the rest. */
export function loadOpenModules(moduleKeys: string[]): string[] {
  try {
    const closed: unknown = JSON.parse(localStorage.getItem(CLOSED_MODULES_KEY) ?? '[]')
    return Array.isArray(closed) ? moduleKeys.filter((key) => !closed.includes(key)) : moduleKeys
  } catch {
    return moduleKeys
  }
}

export function saveOpenModules(moduleKeys: string[], openKeys: string[]): void {
  try {
    localStorage.setItem(CLOSED_MODULES_KEY, JSON.stringify(moduleKeys.filter((key) => !openKeys.includes(key))))
  } catch {
    // Without storage, it only goes back to the default on the next load.
  }
}
