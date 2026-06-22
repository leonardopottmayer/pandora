import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import { getMe } from './users.service'

const ME = `${TEST_API_BASE}/api/v1/identity/me`

const user = { id: 'u1', name: 'Leo', email: 'leo@x.com', username: 'leo' }

describe('users.service getMe', () => {
  it('returns the current user on 200', async () => {
    server.use(http.get(ME, () => HttpResponse.json({ success: true, data: user })))
    const result = await getMe()
    expect(result?.id).toBe('u1')
  })

  it('returns null when the endpoint is not implemented (404)', async () => {
    server.use(http.get(ME, () => new HttpResponse(null, { status: 404 })))
    expect(await getMe()).toBeNull()
  })

  it('returns null when not implemented (501)', async () => {
    server.use(http.get(ME, () => new HttpResponse(null, { status: 501 })))
    expect(await getMe()).toBeNull()
  })

  it('returns null on unauthorized (401)', async () => {
    server.use(http.get(ME, () => new HttpResponse(null, { status: 401 })))
    expect(await getMe()).toBeNull()
  })
})
