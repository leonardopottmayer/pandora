export type IntegrationStatus = 'connected' | 'expired' | 'revoked' | 'needs_consent'

/** A provider the server can talk to, and whether the user has connected it. */
export interface ProviderCatalogItem {
  provider: string
  /** How to connect it: 'oauth' sends the browser to consent, 'api_key' shows a field to paste a key. */
  authKind: string
  /** Friendly name for api_key providers (e.g. "Google Gemini"); null for OAuth. */
  displayName: string | null
  defaultScopes: string[]
  connected: boolean
  status: IntegrationStatus | null
}

/** A connected third-party account, as shown in settings. Never carries a token. */
export interface ExternalAccount {
  id: string
  provider: string
  authKind: string
  displayName: string | null
  scopes: string[]
  status: IntegrationStatus
  lastError: string | null
  connectedAt: string
  lastRefreshedAt: string | null
}

export type IntegrationEventType =
  | 'connected'
  | 'reconnected'
  | 'refresh_failed'
  | 'expired'
  | 'revoked'
  | 'disconnected'

/** One entry of the connection event log — the "why did sync stop" timeline. */
export interface IntegrationEvent {
  id: string
  externalAccountId: string | null
  provider: string
  eventType: IntegrationEventType
  detail: string | null
  occurredAt: string
}

/** Human label for a provider key. */
export function providerLabel(provider: string): string {
  switch (provider) {
    case 'google':
      return 'Google'
    default:
      return provider.charAt(0).toUpperCase() + provider.slice(1)
  }
}
