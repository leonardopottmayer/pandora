import { apiClient } from '@/lib/api/client'
import type {
  AssistantProfile,
  AssistantProvider,
  InterpretResult,
  Invocation,
  ReachabilityResult,
} from '../models'

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

/** Interprets one sentence and, when it maps cleanly to a command, executes it inline. */
export async function interpret(text: string, conversationId?: string): Promise<InterpretResult> {
  const { data } = await apiClient.post<InterpretResult>(`${BASE}/interpret`, {
    text,
    conversationId: conversationId ?? null,
  })
  return data
}

/** Runs a tool call that was held for confirmation. */
export async function confirmInvocation(id: string): Promise<InterpretResult> {
  const { data } = await apiClient.post<InterpretResult>(`${BASE}/invocations/${id}/confirm`)
  return data
}

/** Declines a tool call that was held for confirmation. */
export async function cancelInvocation(id: string): Promise<InterpretResult> {
  const { data } = await apiClient.post<InterpretResult>(`${BASE}/invocations/${id}/cancel`)
  return data
}

/** The user's recent interpretations — the audit trail. */
export async function listInvocations(limit = 50): Promise<Invocation[]> {
  const { data } = await apiClient.get<Invocation[]>(`${BASE}/invocations`, { params: { limit } })
  return data
}
