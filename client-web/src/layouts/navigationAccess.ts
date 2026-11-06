import { allows, isFeatureOn, type Access } from '@/modules/identity/lib/access'
import type { NavModule, NavScreen } from './navigation'

/** The modules and screens the user can open; a module with no screen left is dropped. */
export function visibleModules(modules: NavModule[], access: Access | null): NavModule[] {
  return modules
    .filter((mod) => !mod.feature || isFeatureOn(access, mod.feature))
    .map((mod) => ({ ...mod, screens: mod.screens.filter((screen) => allows(access, screen)) }))
    .filter((mod) => mod.screens.length > 0)
}

export type RouteAccess = 'allowed' | 'feature-off' | 'forbidden'

/**
 * Whether the user may open a path. It belongs to the screen whose path is its longest prefix
 * (`/finances/accounts/42` → Accounts); a path under no screen is allowed.
 */
export function routeAccess(modules: NavModule[], pathname: string, access: Access | null): RouteAccess {
  let match: { mod: NavModule; screen: NavScreen } | null = null
  for (const mod of modules) {
    for (const screen of mod.screens) {
      if (screen.path === '/') continue
      const covers = pathname === screen.path || pathname.startsWith(`${screen.path}/`)
      if (covers && (!match || screen.path.length > match.screen.path.length)) match = { mod, screen }
    }
  }
  if (!match) return 'allowed'

  const feature = match.screen.feature ?? match.mod.feature
  if (feature && !isFeatureOn(access, feature)) return 'feature-off'
  if (match.screen.permission && !allows(access, { permission: match.screen.permission })) return 'forbidden'
  return 'allowed'
}
