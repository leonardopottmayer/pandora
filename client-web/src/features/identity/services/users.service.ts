import { apiClient } from '@/lib/api/client'
import type { CurrentUser } from '../models'

/**
 * Loads the current user. Endpoint not yet implemented on the backend
 * (GET /api/v1/identity/me) — tolerates 404/501 by returning null so the app
 * works before it exists. Once the endpoint ships, the name/email show up
 * automatically.
 */
export async function getMe(): Promise<CurrentUser | null> {
  const res = await apiClient.get<CurrentUser>('/api/v1/identity/me', {
    validateStatus: (status) => status === 200 || status === 401 || status === 404 || status === 501,
  })
  return res.status === 200 ? res.data : null
}
