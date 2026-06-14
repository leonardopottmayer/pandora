import { describe, it, expect, beforeEach } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { apiClient } from './client'
import { ApiResponseError } from './envelope'
import { clearTokens, getAccessToken, setTokens } from './tokenStorage'

const PING = `${TEST_API_BASE}/api/v1.0/finances/ping`
const REFRESH = `${TEST_API_BASE}/api/v1/identity/auth/refresh`

function error401(code = 'Auth.Unauthorized') {
  return HttpResponse.json(
    { success: false, errorCode: code, errorMessage: 'Unauthorized', fieldErrors: null, traceId: null },
    { status: 401 },
  )
}

describe('apiClient 401 refresh', () => {
  beforeEach(() => {
    clearTokens()
  })

  it('refreshes on 401 (even with an error envelope) and replays the request', async () => {
    setTokens('old-access', 'refresh-1')
    let refreshCalled = false

    server.use(
      http.get(PING, ({ request }) => {
        const auth = request.headers.get('Authorization')
        return auth === 'Bearer new-access'
          ? HttpResponse.json({ success: true, data: { ok: true } })
          : error401()
      }),
      http.post(REFRESH, () => {
        refreshCalled = true
        return HttpResponse.json({ success: true, data: { accessToken: 'new-access', refreshToken: 'refresh-2' } })
      }),
    )

    const { data } = await apiClient.get<{ ok: boolean }>('/api/v1.0/finances/ping')
    expect(data).toEqual({ ok: true })
    expect(refreshCalled).toBe(true)
    expect(getAccessToken()).toBe('new-access')
  })

  it('does not attempt refresh when there is no refresh token', async () => {
    let refreshCalled = false
    server.use(
      http.get(PING, () => error401()),
      http.post(REFRESH, () => {
        refreshCalled = true
        return HttpResponse.json({ success: true, data: { accessToken: 'x', refreshToken: 'y' } })
      }),
    )

    await expect(apiClient.get('/api/v1.0/finances/ping')).rejects.toBeInstanceOf(ApiResponseError)
    expect(refreshCalled).toBe(false)
  })

  it('does not attempt refresh for auth routes (bad sign-in)', async () => {
    setTokens('old-access', 'refresh-1')
    let refreshCalled = false
    server.use(
      http.post(`${TEST_API_BASE}/api/v1/identity/auth/signin`, () => error401('Auth.InvalidCredentials')),
      http.post(REFRESH, () => {
        refreshCalled = true
        return HttpResponse.json({ success: true, data: { accessToken: 'x', refreshToken: 'y' } })
      }),
    )

    await expect(
      apiClient.post('/api/v1/identity/auth/signin', { emailOrUsername: 'x', password: 'y' }),
    ).rejects.toBeInstanceOf(ApiResponseError)
    expect(refreshCalled).toBe(false)
  })
})
