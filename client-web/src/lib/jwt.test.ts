import { describe, it, expect } from 'vitest'
import { getUserIdFromToken } from './jwt'

/** Builds a fake JWT with the given payload (base64url, no signature verification). */
function makeToken(payload: Record<string, unknown>): string {
  const b64 = btoa(JSON.stringify(payload)).replace(/\+/g, '-').replace(/\//g, '_')
  return `header.${b64}.signature`
}

describe('getUserIdFromToken', () => {
  it('reads the "Id" claim', () => {
    expect(getUserIdFromToken(makeToken({ Id: 'user-1' }))).toBe('user-1')
  })

  it('falls back to the "sub" claim when "Id" is absent', () => {
    expect(getUserIdFromToken(makeToken({ sub: 'user-2' }))).toBe('user-2')
  })

  it('prefers "Id" over "sub"', () => {
    expect(getUserIdFromToken(makeToken({ Id: 'a', sub: 'b' }))).toBe('a')
  })

  it('returns null when neither claim is present', () => {
    expect(getUserIdFromToken(makeToken({ exp: 123 }))).toBeNull()
  })

  it('returns null for a token without a payload segment', () => {
    expect(getUserIdFromToken('not-a-jwt')).toBeNull()
  })

  it('returns null when the payload is not valid base64/JSON', () => {
    expect(getUserIdFromToken('header.!!!notbase64!!!.sig')).toBeNull()
  })
})
