import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from './msw/server'
import { apiClient } from '@/lib/api/client'
import { ApiResponseError } from '@/lib/api/envelope'
import { TEST_API_BASE } from './constants'

// Smoke test: proves the whole test harness wiring works end-to-end —
// the pinned API base, MSW interception, and the Tars envelope unwrapping
// done by apiClient's interceptors.
describe('test harness', () => {
  it('unwraps the Tars success envelope', async () => {
    server.use(
      http.get(`${TEST_API_BASE}/api/v1.0/ping`, () =>
        HttpResponse.json({ success: true, data: { ok: true } }),
      ),
    )
    const { data } = await apiClient.get<{ ok: boolean }>('/api/v1.0/ping')
    expect(data).toEqual({ ok: true })
  })

  it('rejects with ApiResponseError on the Tars error envelope', async () => {
    server.use(
      http.get(`${TEST_API_BASE}/api/v1.0/boom`, () =>
        HttpResponse.json(
          { success: false, errorCode: 'BOOM', errorMessage: 'Boom!', fieldErrors: null, traceId: null },
          { status: 400 },
        ),
      ),
    )
    await expect(apiClient.get('/api/v1.0/boom')).rejects.toBeInstanceOf(ApiResponseError)
  })
})
