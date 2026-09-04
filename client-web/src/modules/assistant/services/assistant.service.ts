import { apiClient } from '@/lib/api/client'
import type { AssistantProfile, AssistantProvider, ReachabilityResult } from '../models'

const BASE = '/api/v1.0/assistant'

export async function getProfile(): Promise<AssistantProfile> {
  const { data } = await apiClient.get<AssistantProfile>(`${BASE}/profile`)
  return data
}

export async function saveProfile(profile: AssistantProfile): Promise<void> {
  await apiClient.post(`${BASE}/profile`, profile)
}

export async function listProviders(): Promise<AssistantProvider[]> {
  const { data } = await apiClient.get<AssistantProvider[]>(`${BASE}/providers`)
  return data
}

/** Runs a reachability probe against a provider using the user's stored key. Always resolves; the
 *  payload says whether the probe itself succeeded. */
export async function testProvider(provider: string, model?: string): Promise<ReachabilityResult> {
  const { data } = await apiClient.post<ReachabilityResult>(`${BASE}/providers/${provider}/test`, {
    model: model ?? null,
  })
  return data
}
