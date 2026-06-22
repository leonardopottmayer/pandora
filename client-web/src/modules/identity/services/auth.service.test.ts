import { describe, it, expect, beforeEach } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import {
  signIn,
  signUp,
  signOut,
  activateAccount,
  forgotPassword,
  resetPassword,
  changePassword,
  completeMfaChallenge,
} from './auth.service'
import { getAccessToken, getRefreshToken, setTokens } from '@/lib/api/tokenStorage'
import type { SignInResult } from '../models'

const AUTH = `${TEST_API_BASE}/api/v1/identity/auth`
const MFA = `${TEST_API_BASE}/api/v1/identity/mfa`

const tokens = {
  accessToken: 'access-1',
  accessTokenExpiresAt: 0,
  refreshToken: 'refresh-1',
  refreshTokenExpiresAt: '2030-01-01',
}

beforeEach(() => localStorage.clear())

describe('signIn', () => {
  it('stores the tokens when the response carries them', async () => {
    const result: SignInResult = { tokens, mfa: null }
    server.use(http.post(`${AUTH}/signin`, () => HttpResponse.json({ success: true, data: result })))

    const data = await signIn('user@example.com', 'pw')

    expect(data.tokens?.accessToken).toBe('access-1')
    expect(getAccessToken()).toBe('access-1')
    expect(getRefreshToken()).toBe('refresh-1')
  })

  it('does not store tokens when an MFA challenge is returned', async () => {
    const result: SignInResult = { tokens: null, mfa: { ticket: 't1', expiresAt: '2030-01-01' } }
    server.use(http.post(`${AUTH}/signin`, () => HttpResponse.json({ success: true, data: result })))

    const data = await signIn('user@example.com', 'pw')

    expect(data.mfa?.ticket).toBe('t1')
    expect(getAccessToken()).toBeNull()
  })
})

describe('completeMfaChallenge', () => {
  it('stores the tokens returned by the challenge', async () => {
    server.use(http.post(`${MFA}/challenge`, () => HttpResponse.json({ success: true, data: tokens })))

    const data = await completeMfaChallenge('ticket', '123456')

    expect(data.accessToken).toBe('access-1')
    expect(getAccessToken()).toBe('access-1')
  })
})

describe('signOut', () => {
  it('clears tokens after a successful sign-out', async () => {
    setTokens('access-1', 'refresh-1')
    let sentRefresh: unknown
    server.use(
      http.post(`${AUTH}/signout`, async ({ request }) => {
        sentRefresh = (await request.json() as { refreshToken: string }).refreshToken
        return HttpResponse.json({ success: true, data: null })
      }),
    )

    await signOut()

    expect(sentRefresh).toBe('refresh-1')
    expect(getAccessToken()).toBeNull()
    expect(getRefreshToken()).toBeNull()
  })

  it('clears tokens even when the request fails', async () => {
    setTokens('access-1', 'refresh-1')
    server.use(http.post(`${AUTH}/signout`, () => HttpResponse.error()))

    await expect(signOut()).rejects.toBeTruthy()

    expect(getAccessToken()).toBeNull()
  })
})

describe('fire-and-forget auth endpoints', () => {
  it('signUp posts the registration request', async () => {
    let body: unknown
    server.use(
      http.post(`${AUTH}/signup`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    await signUp({ name: 'A', username: 'a', email: 'a@x.com', password: 'pw' })
    expect(body).toMatchObject({ email: 'a@x.com' })
  })

  it('activateAccount posts the token', async () => {
    let body: unknown
    server.use(
      http.post(`${AUTH}/activate`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    await activateAccount('tok')
    expect(body).toEqual({ token: 'tok' })
  })

  it('forgotPassword posts the email', async () => {
    let body: unknown
    server.use(
      http.post(`${AUTH}/password/forgot`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    await forgotPassword('a@x.com')
    expect(body).toEqual({ email: 'a@x.com' })
  })

  it('resetPassword posts token and new password', async () => {
    let body: unknown
    server.use(
      http.post(`${AUTH}/password/reset`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    await resetPassword('tok', 'NewPw1!')
    expect(body).toEqual({ token: 'tok', newPassword: 'NewPw1!' })
  })

  it('changePassword posts current and new password', async () => {
    let body: unknown
    server.use(
      http.post(`${AUTH}/password/change`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    await changePassword('Old1!', 'New1!')
    expect(body).toEqual({ currentPassword: 'Old1!', newPassword: 'New1!' })
  })
})
