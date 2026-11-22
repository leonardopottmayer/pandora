export type IntegrationStatus = 'connected' | 'expired' | 'revoked' | 'needs-consent'

/** How an account authenticates: an OAuth consent flow, a pasted API key, or a username + password. */
export type AuthKind = 'oauth' | 'api-key' | 'password'

/** A provider the server can talk to, and how many accounts the user has at it. */
export interface ProviderCatalogItem {
  provider: string
  /** How to add an account: 'oauth' sends the browser to consent, the others show a form. */
  authKind: AuthKind
  /** Friendly name for typed-in providers (e.g. "Google Gemini"); null for OAuth. */
  displayName: string | null
  defaultScopes: string[]
  accountCount: number
}

/** One place another module uses an account. `module`/`purpose` are keys the screen translates. */
export interface AccountUsage {
  module: string
  purpose: string
  /** Optional user data shown as-is (a mailbox address, a calendar name). */
  detail: string | null
}

/** A connected third-party account, as shown in settings. Never carries a token. */
export interface ExternalAccount {
  id: string
  provider: string
  authKind: AuthKind
  /** The user's name for the account ("Personal", "Work"). */
  label: string
  /** A technical hint: the email, the username or the masked key. */
  displayName: string | null
  scopes: string[]
  status: IntegrationStatus
  lastError: string | null
  connectedAt: string
  lastRefreshedAt: string | null
  usages: AccountUsage[]
}

export type IntegrationEventType =
  | 'connected'
  | 'reconnected'
  | 'refresh-failed'
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
    case 'imap':
      return 'IMAP'
    default:
      return provider.charAt(0).toUpperCase() + provider.slice(1)
  }
}
