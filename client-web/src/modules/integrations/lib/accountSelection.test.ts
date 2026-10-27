import { describe, it, expect } from 'vitest'
import type { ExternalAccount } from '../models'
import { eligibleAccounts, preselectedAccount } from './accountSelection'

function account(id: string, overrides: Partial<ExternalAccount> = {}): ExternalAccount {
  return {
    id,
    provider: 'gemini',
    authKind: 'api-key',
    label: id,
    displayName: null,
    scopes: [],
    status: 'connected',
    lastError: null,
    connectedAt: '2026-10-26T00:00:00Z',
    lastRefreshedAt: null,
    usages: [],
    ...overrides,
  }
}

const gemini = { providers: ['gemini'], authKind: 'api-key' as const }

describe('account selection', () => {
  it('preselects the only eligible account', () => {
    expect(preselectedAccount([account('a'), account('g', { provider: 'google', authKind: 'oauth' })], gemini)).toBe('a')
  })

  it('preselects nothing when two accounts are eligible', () => {
    expect(preselectedAccount([account('a'), account('b')], gemini)).toBeNull()
  })

  it('does not count an account missing a required scope or not connected', () => {
    const mail = { providers: ['google'], requiredScopes: ['gmail.modify'] }
    const accounts = [
      account('no-scope', { provider: 'google', authKind: 'oauth', scopes: ['email'] }),
      account('revoked', { provider: 'google', authKind: 'oauth', scopes: ['gmail.modify'], status: 'revoked' }),
      account('ok', { provider: 'google', authKind: 'oauth', scopes: ['email', 'gmail.modify'] }),
    ]

    expect(eligibleAccounts(accounts, mail).map((a) => a.id)).toEqual(['ok'])
    expect(preselectedAccount(accounts, mail)).toBe('ok')
  })
})
