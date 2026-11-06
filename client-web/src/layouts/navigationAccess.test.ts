import { describe, expect, it } from 'vitest'
import { accessOf } from '@/modules/identity/lib/access'
import { navigationModules } from './navigation'
import { routeAccess, visibleModules } from './navigationAccess'

const member = accessOf({
  id: 'u1',
  name: 'Leo',
  email: 'leo@x.com',
  username: 'leo',
  features: ['finances', 'notes'],
  permissions: ['finances.accounts.read', 'notes.spaces.read', 'notes.media.read'],
})

describe('visibleModules', () => {
  it('keeps the screens the user may open and drops empty or switched-off modules', () => {
    const keys = visibleModules(navigationModules, member).map((m) => [m.key, m.screens.map((s) => s.key)])
    expect(keys).toEqual([
      ['dashboard', ['home']],
      ['finances', ['fin-accounts']],
      ['notes', ['notes-spaces', 'notes-library']],
      ['account', ['profile', 'security', 'devices', 'settings']],
    ])
  })

  it('shows everything while the access is unknown', () => {
    expect(visibleModules(navigationModules, null)).toHaveLength(navigationModules.length)
  })
})

describe('routeAccess', () => {
  it('judges a path by the screen it falls under', () => {
    expect(routeAccess(navigationModules, '/finances/accounts/42', member)).toBe('allowed')
    expect(routeAccess(navigationModules, '/finances/cards', member)).toBe('forbidden')
    expect(routeAccess(navigationModules, '/agenda/settings', member)).toBe('feature-off')
    expect(routeAccess(navigationModules, '/admin/users', member)).toBe('forbidden')
    expect(routeAccess(navigationModules, '/account/password', member)).toBe('allowed')
    expect(routeAccess(navigationModules, '/', member)).toBe('allowed')
  })
})
