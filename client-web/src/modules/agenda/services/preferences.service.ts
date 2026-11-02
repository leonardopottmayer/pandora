import { apiClient } from '@/lib/api/client'

const BASE = '/api/v1.0/agenda/preferences'

export interface AgendaPreferences {
  /** Signed minutes, relative to the item (e.g. -15 = fifteen minutes before). */
  defaultAlertOffsetMinutes: number
}

/** The defaults when never saved. */
export async function getPreferences(): Promise<AgendaPreferences> {
  const { data } = await apiClient.get<AgendaPreferences>(BASE)
  return data
}

export async function savePreferences(body: AgendaPreferences): Promise<AgendaPreferences> {
  const { data } = await apiClient.put<AgendaPreferences>(BASE, body)
  return data
}
