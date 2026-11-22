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

export interface StartConnectionOptions {
  /** Scopes to request; the provider's defaults when omitted. */
  scopes?: string[]
  /** Reconnect (or widen the scopes of) this account instead of adding a new one. */
  accountId?: string
}

/** Starts a connection and returns the provider consent URL to send the browser to. */
export async function startConnection(
  provider: string,
  redirectAfter: string,
  options: StartConnectionOptions = {},
): Promise<string> {
  const { data } = await apiClient.post<string>(`${BASE}/${provider}/connect`, {
    redirectAfter,
    scopes: options.scopes ?? null,
    accountId: options.accountId ?? null,
  })
  return data
}

export async function disconnectAccount(id: string): Promise<void> {
  await apiClient.delete(`${BASE}/accounts/${id}`)
}

/** Adds an API key (e.g. Gemini) as a new account and returns its id. */
export async function saveApiKey(provider: string, apiKey: string, label?: string): Promise<string> {
  const { data } = await apiClient.put<string>(`${BASE}/${provider}/api-key`, { apiKey, label: label || null })
  return data
}

/** Stores a username + password (Titan, IMAP); the same username again replaces its password. Returns the account id. */
export async function savePassword(
  provider: string,
  username: string,
  password: string,
  label?: string,
): Promise<string> {
  const { data } = await apiClient.put<string>(`${BASE}/${provider}/password`, {
    username,
    password,
    label: label || null,
  })
  return data
}

/** Replaces the API key or password of one account. */
export async function replaceSecret(id: string, secret: string): Promise<void> {
  await apiClient.put(`${BASE}/accounts/${id}/secret`, { secret })
}

export async function renameAccount(id: string, label: string): Promise<void> {
  await apiClient.patch(`${BASE}/accounts/${id}`, { label })
}
