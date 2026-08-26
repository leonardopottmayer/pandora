export type IntegrationStatus = 'connected' | 'expired' | 'revoked' | 'needs_consent'

/** A provider the server can talk to, and whether the user has connected it. */
export interface ProviderCatalogItem {
  provider: string
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

/** Human label for a provider key. */
export function providerLabel(provider: string): string {
  switch (provider) {
    case 'google':
      return 'Google'
    default:
      return provider.charAt(0).toUpperCase() + provider.slice(1)
  }
}
