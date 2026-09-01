import { apiClient } from '@/lib/api/client'
import type { ExternalAccount, IntegrationEvent, ProviderCatalogItem } from '../models'

const BASE = '/api/v1.0/integrations'

export async function listProviders(): Promise<ProviderCatalogItem[]> {
  const { data } = await apiClient.get<ProviderCatalogItem[]>(`${BASE}/providers`)
  return data
}

export async function listAccounts(): Promise<ExternalAccount[]> {
  const { data } = await apiClient.get<ExternalAccount[]>(`${BASE}/accounts`)
  return data
}

export async function listEvents(limit = 50): Promise<IntegrationEvent[]> {
  const { data } = await apiClient.get<IntegrationEvent[]>(`${BASE}/events`, { params: { limit } })
  return data
}

/** Starts a connection and returns the provider consent URL to send the browser to. */
export async function startConnection(provider: string, redirectAfter: string): Promise<string> {
  const { data } = await apiClient.post<string>(`${BASE}/${provider}/connect`, { redirectAfter })
  return data
}

export async function disconnectAccount(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/accounts/${id}`)
}
