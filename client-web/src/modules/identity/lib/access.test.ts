import { describe, expect, it } from 'vitest'
import { accessOf, allows } from './access'

const user = {
  id: 'u1',
  name: 'Leo',
  email: 'leo@x.com',
  username: 'leo',
  features: ['finances'],
  permissions: ['finances.cards.read'],
}

describe('access', () => {
  it('needs the feature on and the permission held', () => {
    const access = accessOf(user)
    expect(allows(access, { feature: 'finances', permission: 'finances.cards.read' })).toBe(true)
    expect(allows(access, { feature: 'notes' })).toBe(false)
    expect(allows(access, { permission: 'finances.cards.delete' })).toBe(false)
    expect(allows(access, {})).toBe(true)
  })

  it('hides nothing while the access is unknown', () => {
    expect(accessOf(null)).toBeNull()
    expect(accessOf({ ...user, features: undefined as unknown as string[] })).toBeNull()
    expect(allows(null, { feature: 'notes', permission: 'notes.pages.read' })).toBe(true)
  })
})
