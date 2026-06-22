import type { ReactNode } from 'react'
import { describe, it, expect, beforeEach } from 'vitest'
import { http, HttpResponse } from 'msw'
import { renderHook, waitFor, act } from '@testing-library/react'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { setTokens, getAccessToken } from '@/lib/api/tokenStorage'
import { AuthProvider } from './AuthProvider'
import { useAuth } from './auth-context'

const IDENTITY = `${TEST_API_BASE}/api/v1/identity`
const user = { id: 'u1', name: 'Leo', email: 'leo@x.com', username: 'leo' }

function wrapper({ children }: { children: ReactNode }) {
  return <AuthProvider>{children}</AuthProvider>
}

beforeEach(() => localStorage.clear())

describe('AuthProvider', () => {
  it('starts unauthenticated when there is no token', async () => {
    server.use(http.get(`${IDENTITY}/me`, () => new HttpResponse(null, { status: 401 })))
    const { result } = renderHook(() => useAuth(), { wrapper })
    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.isAuthenticated).toBe(false)
    expect(result.current.user).toBeNull()
  })

  it('loads the current user when a token already exists', async () => {
    setTokens('access-1', 'refresh-1')
    server.use(http.get(`${IDENTITY}/me`, () => HttpResponse.json({ success: true, data: user })))
    const { result } = renderHook(() => useAuth(), { wrapper })
    await waitFor(() => expect(result.current.user?.id).toBe('u1'))
    expect(result.current.isAuthenticated).toBe(true)
  })

  it('authenticates and stores the login label on sign-in with tokens', async () => {
    server.use(
      http.post(`${IDENTITY}/auth/signin`, () =>
        HttpResponse.json({
          success: true,
          data: { tokens: { accessToken: 'a', accessTokenExpiresAt: 0, refreshToken: 'r', refreshTokenExpiresAt: '2030' }, mfa: null },
        }),
      ),
      http.get(`${IDENTITY}/me`, () => HttpResponse.json({ success: true, data: user })),
    )
    const { result } = renderHook(() => useAuth(), { wrapper })
    await waitFor(() => expect(result.current.isLoading).toBe(false))

    await act(async () => {
      await result.current.login('leo', 'pw')
    })

    expect(result.current.isAuthenticated).toBe(true)
    expect(result.current.loginLabel).toBe('leo')
    expect(getAccessToken()).toBe('a')
  })

  it('does not authenticate when sign-in returns an MFA challenge', async () => {
    server.use(
      http.post(`${IDENTITY}/auth/signin`, () =>
        HttpResponse.json({ success: true, data: { tokens: null, mfa: { ticket: 't1', expiresAt: '2030' } } }),
      ),
      http.get(`${IDENTITY}/me`, () => new HttpResponse(null, { status: 401 })),
    )
    const { result } = renderHook(() => useAuth(), { wrapper })
    await waitFor(() => expect(result.current.isLoading).toBe(false))

    let signInResult: Awaited<ReturnType<typeof result.current.login>> | undefined
    await act(async () => {
      signInResult = await result.current.login('leo', 'pw')
    })

    expect(signInResult?.mfa?.ticket).toBe('t1')
    expect(result.current.isAuthenticated).toBe(false)
  })

  it('clears the session on logout', async () => {
    setTokens('access-1', 'refresh-1')
    server.use(
      http.get(`${IDENTITY}/me`, () => HttpResponse.json({ success: true, data: user })),
      http.post(`${IDENTITY}/auth/signout`, () => HttpResponse.json({ success: true, data: null })),
    )
    const { result } = renderHook(() => useAuth(), { wrapper })
    await waitFor(() => expect(result.current.isAuthenticated).toBe(true))

    await act(async () => {
      await result.current.logout()
    })

    expect(result.current.isAuthenticated).toBe(false)
    expect(result.current.user).toBeNull()
    expect(getAccessToken()).toBeNull()
  })
})
