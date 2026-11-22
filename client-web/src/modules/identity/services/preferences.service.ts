import { apiClient } from '@/lib/api/client'
import type { UserPreferences } from '../models'

const PREFERENCES_BASE = '/api/v1/identity/preferences'

export async function getPreferences(): Promise<UserPreferences> {
  const { data } = await apiClient.get<UserPreferences>(PREFERENCES_BASE)
  return data
}

export async function upsertPreferences(prefs: UserPreferences): Promise<void> {
  await apiClient.put(PREFERENCES_BASE, prefs)
}
