import { describe, it, expect } from 'vitest'
import { http, HttpResponse } from 'msw'
import { server } from '@/test/msw/server'
import { TEST_API_BASE } from '@/test/constants'
import {
  getMfaStatus,
  setupMfa,
  enableMfa,
  disableMfa,
  regenerateRecoveryCodes,
} from './mfa.service'

const MFA = `${TEST_API_BASE}/api/v1/identity/mfa`

describe('mfa.service', () => {
  it('reads the MFA status', async () => {
    server.use(
      http.get(`${MFA}/status`, () =>
        HttpResponse.json({ success: true, data: { enabled: true, remainingRecoveryCodes: 5 } }),
      ),
    )
    const status = await getMfaStatus()
    expect(status.enabled).toBe(true)
    expect(status.remainingRecoveryCodes).toBe(5)
  })

  it('starts enrollment returning secret and otpauth uri', async () => {
    server.use(
      http.post(`${MFA}/setup`, () =>
        HttpResponse.json({ success: true, data: { secret: 'BASE32', otpauthUri: 'otpauth://x' } }),
      ),
    )
    const setup = await setupMfa()
    expect(setup.secret).toBe('BASE32')
    expect(setup.otpauthUri).toBe('otpauth://x')
  })

  it('enables MFA with a TOTP code and returns recovery codes', async () => {
    let body: unknown
    server.use(
      http.post(`${MFA}/enable`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: { recoveryCodes: ['a', 'b'] } })
      }),
    )
    const result = await enableMfa('123456')
    expect(body).toEqual({ code: '123456' })
    expect(result.recoveryCodes).toHaveLength(2)
  })

  it('disables MFA with password and code', async () => {
    let body: unknown
    server.use(
      http.post(`${MFA}/disable`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({ success: true, data: null })
      }),
    )
    await disableMfa('pw', '123456')
    expect(body).toEqual({ password: 'pw', code: '123456' })
  })

  it('regenerates recovery codes', async () => {
    server.use(
      http.post(`${MFA}/recovery-codes`, () =>
        HttpResponse.json({ success: true, data: { recoveryCodes: ['x', 'y', 'z'] } }),
      ),
    )
    const result = await regenerateRecoveryCodes('pw', '123456')
    expect(result.recoveryCodes).toHaveLength(3)
  })
})
