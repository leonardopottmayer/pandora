import { useState } from 'react'
import type { GraphFamily, GraphPreferences } from '../lib/graphGroups'

const defaults = (): GraphPreferences => ({ showGroups: false, families: [] })
export const graphPreferencesKey = (userId: string) => `pandora.notes_graph.${userId}`

function isFamily(value: unknown): value is GraphFamily {
  if (!value || typeof value !== 'object') return false
  const family = value as Partial<GraphFamily>
  return typeof family.id === 'string' && typeof family.name === 'string' &&
    typeof family.color === 'string' && /^#[\da-f]{6}$/i.test(family.color) &&
    Array.isArray(family.tagIds) && family.tagIds.every((id) => typeof id === 'string')
}

function read(userId: string | null): GraphPreferences {
  if (!userId) return defaults()
  try {
    const value = JSON.parse(localStorage.getItem(graphPreferencesKey(userId)) ?? 'null')
    if (value && typeof value.showGroups === 'boolean' && Array.isArray(value.families) && value.families.every(isFamily)) {
      return { showGroups: value.showGroups, families: value.families }
    }
  } catch { /* Blocked storage or invalid JSON: use the default view. */ }
  return defaults()
}

/** A device preference like the editor mode, separated by account; no write during initial loading. */
export function useGraphPreferences(userId: string | null) {
  const [state, setState] = useState(() => ({ userId, preferences: read(userId) }))
  if (state.userId !== userId) setState({ userId, preferences: read(userId) })

  function update(preferences: GraphPreferences) {
    setState({ userId, preferences })
    if (!userId) return
    try {
      localStorage.setItem(graphPreferencesKey(userId), JSON.stringify(preferences))
    } catch { /* The choice still lasts for this visit when storage is blocked. */ }
  }
  return { preferences: state.preferences, update }
}
