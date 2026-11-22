import { useContext, useMemo } from 'react'
import { AuthContext } from '../context/auth-context'
import { accessOf, allows, holds, isFeatureOn, type Access, type AccessRule } from '../lib/access'

/**
 * The signed-in user's features and permissions, from `/me`. Outside an `AuthProvider` (a screen
 * rendered alone in a test) the access is unknown, so nothing is hidden.
 */
export function useAccess(): Access | null {
  const user = useContext(AuthContext)?.user ?? null
  return useMemo(() => accessOf(user), [user])
}

export function useFeature(feature: string): boolean {
  return isFeatureOn(useAccess(), feature)
}

export function usePermission(permission: string): boolean {
  return holds(useAccess(), permission)
}

export function useAllows(rule: AccessRule): boolean {
  return allows(useAccess(), rule)
}
