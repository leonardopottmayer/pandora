import type { CurrentUser } from '../models'

/** What a screen, a menu entry or a button needs: a feature switched on and/or a permission held. */
export interface AccessRule {
  feature?: string
  permission?: string
}

export interface Access {
  features: ReadonlySet<string>
  permissions: ReadonlySet<string>
}

/**
 * The user's access, or `null` while it is unknown (not loaded, or `/me` failed). Unknown access hides
 * nothing: hiding is only comfort, the backend still refuses what is not allowed.
 */
export function accessOf(user: CurrentUser | null): Access | null {
  if (!user?.features || !user.permissions) return null
  return { features: new Set(user.features), permissions: new Set(user.permissions) }
}

export function isFeatureOn(access: Access | null, feature: string): boolean {
  return access === null || access.features.has(feature)
}

export function holds(access: Access | null, permission: string): boolean {
  return access === null || access.permissions.has(permission)
}

export function allows(access: Access | null, rule: AccessRule): boolean {
  return (!rule.feature || isFeatureOn(access, rule.feature)) && (!rule.permission || holds(access, rule.permission))
}
